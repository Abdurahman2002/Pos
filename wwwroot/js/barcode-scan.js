(function () {
    var panel = document.querySelector('.barcode-scan');
    if (!panel) {
        return;
    }

    var form = panel.closest('form') || document;
    var resolveUrl = panel.getAttribute('data-resolve-url');
    var mapUrl = panel.getAttribute('data-map-url');
    var mode = panel.getAttribute('data-mode') || 'receipt';
    var warehouseField = panel.getAttribute('data-warehouse-field') || 'WarehouseId';

    var input = document.getElementById('barcode-input');
    var resolveBtn = document.getElementById('barcode-resolve');
    var cameraBtn = document.getElementById('barcode-camera');
    var warningEl = document.getElementById('barcode-warning');

    var cameraModalEl = document.getElementById('barcode-camera-modal');
    var videoEl = document.getElementById('barcode-video');

    var mapModalEl = document.getElementById('barcode-map-modal');
    var mapCodeEl = document.getElementById('barcode-map-code');
    var mapItemEl = document.getElementById('barcode-map-item');
    var mapSaveBtn = document.getElementById('barcode-map-save');
    var mapErrorEl = document.getElementById('barcode-map-error');

    var pendingMapping = null;
    var reader = null;
    var cameraActive = false;
    var lastScanText = '';
    var lastScanAt = 0;
    var cameraModal = cameraModalEl ? new bootstrap.Modal(cameraModalEl) : null;
    var mapModal = mapModalEl ? new bootstrap.Modal(mapModalEl) : null;

    function getToken() {
        var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    function getWarehouseId() {
        var field = form.querySelector('[name="' + warehouseField + '"]');
        return field ? field.value : '';
    }

    function showWarning(message) {
        if (!warningEl) {
            return;
        }
        warningEl.textContent = message;
        warningEl.classList.remove('d-none');
    }

    function clearWarning() {
        if (!warningEl) {
            return;
        }
        warningEl.textContent = '';
        warningEl.classList.add('d-none');
    }

    function setSelectValue(select, value) {
        if (!select) {
            return;
        }
        if (select.tomselect) {
            select.tomselect.setValue(value || '', true);
        } else {
            select.value = value || '';
        }
        select.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function getRowFields(row) {
        return {
            itemSelect: row.querySelector('.item-select'),
            batchInput: row.querySelector('input[name$=".BatchNo"]'),
            expiryInput: row.querySelector('input[name$=".ExpiryDate"]'),
            qtyInput: row.querySelector('input[name$=".Quantity"]')
        };
    }

    function normalizeText(value) {
        return (value || '').trim().toLowerCase();
    }

    function findMatchingRow(itemId, batchNo, expiryDate) {
        var rows = form.querySelectorAll('#lines-table tbody tr');
        for (var i = 0; i < rows.length; i++) {
            var fields = getRowFields(rows[i]);
            if (!fields.itemSelect || fields.itemSelect.value !== itemId) {
                continue;
            }

            if (mode === 'receipt') {
                var batchMatches = normalizeText(fields.batchInput ? fields.batchInput.value : '') === normalizeText(batchNo);
                var expiryMatches = normalizeText(fields.expiryInput ? fields.expiryInput.value : '') === normalizeText(expiryDate);
                if (batchMatches && expiryMatches) {
                    return rows[i];
                }
            } else {
                var hiddenBatch = normalizeText(fields.batchInput ? fields.batchInput.value : '');
                var hiddenExpiry = normalizeText(fields.expiryInput ? fields.expiryInput.value : '');
                if (hiddenBatch === normalizeText(batchNo) && hiddenExpiry === normalizeText(expiryDate)) {
                    return rows[i];
                }
            }
        }
        return null;
    }

    function getEmptyRow() {
        var rows = form.querySelectorAll('#lines-table tbody tr');
        for (var i = 0; i < rows.length; i++) {
            var fields = getRowFields(rows[i]);
            if (fields.itemSelect && !fields.itemSelect.value) {
                return rows[i];
            }
        }
        return null;
    }

    function addRow() {
        var addBtn = form.querySelector('#add-line');
        if (addBtn) {
            addBtn.click();
        }
        var rows = form.querySelectorAll('#lines-table tbody tr');
        return rows.length ? rows[rows.length - 1] : null;
    }

    function setQuantity(qtyInput, incrementBy) {
        if (!qtyInput) {
            return;
        }
        var current = parseInt(qtyInput.value || '0', 10);
        var next = current > 0 ? current + incrementBy : incrementBy;
        qtyInput.value = next.toString();
        qtyInput.dispatchEvent(new Event('change', { bubbles: true }));
        qtyInput.focus();
        qtyInput.select();
    }

    function applyResult(result) {
        clearWarning();

        if (!result || !result.itemId) {
            showWarning('Barcode resolved but item was not found.');
            return;
        }

        if (result.batchAvailable === false) {
            showWarning('Scanned item is not available in current stock.');
            return;
        }

        var row = findMatchingRow(result.itemId, result.batchNo, result.expiryDate);
        if (row) {
            var fields = getRowFields(row);
            setQuantity(fields.qtyInput, 1);
            return;
        }

        row = getEmptyRow();
        if (!row) {
            row = addRow();
        }

        if (!row) {
            showWarning('Unable to add a new line.');
            return;
        }

        var fieldsNew = getRowFields(row);
        setSelectValue(fieldsNew.itemSelect, result.itemId);

        if (mode === 'receipt') {
            if (fieldsNew.batchInput) {
                fieldsNew.batchInput.value = result.batchNo || '';
            }
            if (fieldsNew.expiryInput) {
                fieldsNew.expiryInput.value = result.expiryDate || '';
            }
        } else {
            if (fieldsNew.batchInput) {
                fieldsNew.batchInput.value = result.batchNo || '';
            }
            if (fieldsNew.expiryInput) {
                fieldsNew.expiryInput.value = result.expiryDate || '';
            }
        }

        setQuantity(fieldsNew.qtyInput, 1);
    }

    async function resolveBarcode(code) {
        clearWarning();
        if (!resolveUrl) {
            showWarning('Barcode service is not configured.');
            return;
        }

        var payload = {
            code: code,
            warehouseId: getWarehouseId(),
            mode: mode
        };

        var response = await fetch(resolveUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            showWarning('Unable to resolve barcode.');
            return;
        }

        var data = await response.json();
        if (data.requiresMapping) {
            pendingMapping = data;
            openMapModal(data);
            return;
        }

        if (!data.success) {
            showWarning(data.error || 'Unable to resolve barcode.');
            return;
        }

        applyResult(data);
    }

    function openMapModal(data) {
        if (!mapModal || !mapCodeEl || !mapItemEl) {
            showWarning('Mapping UI is not available.');
            return;
        }

        mapCodeEl.textContent = data.code || '';
        mapItemEl.value = '';
        if (mapItemEl.tomselect) {
            mapItemEl.tomselect.clear();
        }
        if (mapErrorEl) {
            mapErrorEl.classList.add('d-none');
            mapErrorEl.textContent = '';
        }
        mapModal.show();
    }

    async function saveMapping() {
        if (!pendingMapping) {
            return;
        }

        var itemId = mapItemEl ? mapItemEl.value : '';
        if (!itemId) {
            if (mapErrorEl) {
                mapErrorEl.textContent = 'Select an item first.';
                mapErrorEl.classList.remove('d-none');
            }
            return;
        }

        var payload = {
            code: pendingMapping.code,
            codeType: pendingMapping.codeType,
            itemId: itemId
        };

        var response = await fetch(mapUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            if (mapErrorEl) {
                mapErrorEl.textContent = 'Unable to save mapping.';
                mapErrorEl.classList.remove('d-none');
            }
            return;
        }

        var result = await response.json();
        if (!result.success) {
            if (mapErrorEl) {
                mapErrorEl.textContent = result.error || 'Unable to save mapping.';
                mapErrorEl.classList.remove('d-none');
            }
            return;
        }

        if (mapModal) {
            mapModal.hide();
        }

        applyResult({
            itemId: itemId,
            batchNo: pendingMapping.batchNo,
            expiryDate: pendingMapping.expiryDate
        });
        pendingMapping = null;
    }

    function startCamera() {
        if (!cameraModal || !videoEl) {
            return;
        }

        if (!window.ZXing || !ZXing.BrowserMultiFormatReader) {
            showWarning('Camera scanning is not available.');
            return;
        }

        if (!reader) {
            reader = new ZXing.BrowserMultiFormatReader();
        }

        stopCamera();
        clearWarning();
        if (input) {
            input.value = '';
        }
        lastScanText = '';
        lastScanAt = 0;
        cameraActive = true;

        cameraModal.show();

        reader.decodeFromVideoDevice(null, videoEl, function (result, err) {
            if (!cameraActive || !result || !result.text) {
                return;
            }

            var now = Date.now();
            if (result.text === lastScanText && now - lastScanAt < 1500) {
                return;
            }

            lastScanText = result.text;
            lastScanAt = now;

            stopCamera();
            if (cameraModal) {
                cameraModal.hide();
            }
            if (input) {
                input.value = result.text;
            }
            resolveBarcode(result.text);
        });
    }

    function stopCamera() {
        if (reader) {
            reader.reset();
        }
        cameraActive = false;
    }

    if (resolveBtn) {
        resolveBtn.addEventListener('click', function () {
            var code = input ? input.value.trim() : '';
            if (!code) {
                showWarning('Scan or enter a barcode first.');
                return;
            }
            resolveBarcode(code);
        });
    }

    if (cameraBtn) {
        cameraBtn.addEventListener('click', function () {
            startCamera();
        });
    }

    if (input) {
        input.addEventListener('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
                var code = input.value.trim();
                if (code) {
                    resolveBarcode(code);
                }
            }
        });
    }

    if (mapSaveBtn) {
        mapSaveBtn.addEventListener('click', saveMapping);
    }

    if (cameraModalEl) {
        cameraModalEl.addEventListener('hidden.bs.modal', function () {
            stopCamera();
        });
    }
})();
