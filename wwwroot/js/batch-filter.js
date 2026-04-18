(function () {
    function getForm(el) {
        return el.closest('form');
    }

    function getWarehouseId(form) {
        var field = form.getAttribute('data-warehouse-field');
        if (!field) {
            return '';
        }

        var input = form.querySelector('[name="' + field + '"]');
        return input ? input.value : '';
    }

    function getBatchUrl(form) {
        return form.getAttribute('data-batch-url');
    }

    function isManualEnabled(form) {
        var toggle = form.querySelector('#manual-batch-toggle');
        return toggle ? toggle.checked : false;
    }

    function clearBatchOptions(select) {
        if (select.tomselect) {
            select.tomselect.clear();
            select.tomselect.clearOptions();
            select.tomselect.addOption({ value: '', text: 'Auto (FEFO)' });
            select.tomselect.refreshOptions(false);
            return;
        }

        select.innerHTML = '';
        var option = document.createElement('option');
        option.value = '';
        option.textContent = 'Auto (FEFO)';
        select.appendChild(option);
    }

    function setBatchOptions(select, options, selectedValue) {
        clearBatchOptions(select);

        if (select.tomselect) {
            options.forEach(function (option) {
                select.tomselect.addOption(option);
            });
            select.tomselect.refreshOptions(false);
            if (selectedValue) {
                select.tomselect.setValue(selectedValue, true);
            }
            return;
        }

        options.forEach(function (option) {
            var opt = document.createElement('option');
            opt.value = option.value;
            opt.textContent = option.text;
            select.appendChild(opt);
        });

        if (selectedValue) {
            select.value = selectedValue;
        }
    }

    async function fetchBatchOptions(form, itemId) {
        var url = getBatchUrl(form);
        if (!url || !itemId) {
            return [];
        }

        var params = new URLSearchParams({ itemId: itemId });
        var warehouseId = getWarehouseId(form);
        if (warehouseId) {
            params.append('warehouseId', warehouseId);
        }

        var connector = url.indexOf('?') >= 0 ? '&' : '?';
        var response = await fetch(url + connector + params.toString(), {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (!response.ok) {
            return [];
        }

        return await response.json();
    }

    async function refreshRow(row) {
        var form = row.closest('form');
        if (!form || !isManualEnabled(form)) {
            return;
        }

        var itemSelect = row.querySelector('.item-select');
        var batchSelect = row.querySelector('.batch-select');
        if (!itemSelect || !batchSelect || !itemSelect.value) {
            return;
        }

        var previousValue = batchSelect.tomselect ? batchSelect.tomselect.getValue() : batchSelect.value;
        var options = await fetchBatchOptions(form, itemSelect.value);
        var hasPrevious = previousValue && options.some(function (option) {
            return option.value === previousValue;
        });
        setBatchOptions(batchSelect, options, hasPrevious ? previousValue : '');
        row.setAttribute('data-batch-loaded-for', itemSelect.value);
    }

    function refreshAll(form) {
        var rows = form.querySelectorAll('#lines-table tbody tr');
        rows.forEach(function (row) {
            var itemSelect = row.querySelector('.item-select');
            if (!itemSelect || !itemSelect.value) {
                return;
            }

            var loadedFor = row.getAttribute('data-batch-loaded-for');
            if (loadedFor === itemSelect.value) {
                return;
            }

            refreshRow(row);
        });
    }

    function bindItemSelect(select) {
        if (!select || select.dataset.batchBound === 'true') {
            return;
        }

        select.dataset.batchBound = 'true';

        select.addEventListener('change', function () {
            var row = select.closest('tr');
            if (row) {
                refreshRow(row);
            }
        });

        if (select.tomselect) {
            select.tomselect.on('change', function () {
                var row = select.closest('tr');
                if (row) {
                    refreshRow(row);
                }
            });
        }
    }

    function bindForm(form) {
        if (!form) {
            return;
        }

        form.querySelectorAll('.item-select').forEach(bindItemSelect);
    }

    document.addEventListener('change', function (event) {
        if (event.target && event.target.classList.contains('item-select')) {
            var row = event.target.closest('tr');
            if (row) {
                refreshRow(row);
            }
            return;
        }

        if (event.target && event.target.id === 'manual-batch-toggle') {
            var form = getForm(event.target);
            if (form && event.target.checked) {
                refreshAll(form);
            }
            return;
        }

        if (event.target && event.target.name) {
            var form = getForm(event.target);
            if (form && event.target.name === form.getAttribute('data-warehouse-field')) {
                refreshAll(form);
            }
        }
    });

    document.addEventListener('ts:refresh', function () {
        document.querySelectorAll('form[data-batch-url]').forEach(function (form) {
            bindForm(form);
            if (isManualEnabled(form)) {
                refreshAll(form);
            }
        });
    });

    document.querySelectorAll('form[data-batch-url]').forEach(bindForm);
})();
