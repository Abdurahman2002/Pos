(function () {

    // ---------------------------------------------------------------------
    // Local draft storage (used by the purchase invoice screen).
    // The base key is per user; a per-tab suffix keeps concurrent tabs from
    // overwriting each other while still surviving reloads and the login
    // redirect inside the same tab.
    // ---------------------------------------------------------------------
    var LOCAL_DRAFT_TAB_ID_STORAGE_KEY = 'pos.localDraft.tabId';
    var LOCAL_DRAFT_HEARTBEAT_PREFIX = 'pos.localDraft.alive.';
    var LOCAL_DRAFT_HEARTBEAT_INTERVAL_MS = 20000;
    var LOCAL_DRAFT_HEARTBEAT_TTL_MS = 120000;
    var localDraftMemoryTabId = '';
    var localDraftHeartbeatStarted = false;

    function createLocalDraftTabId() {
        try {
            if (window.crypto && typeof window.crypto.randomUUID === 'function') {
                return window.crypto.randomUUID();
            }
        } catch (_) { }

        return 't' + Date.now().toString(36) + Math.random().toString(36).slice(2, 10);
    }

    function getLocalDraftTabId() {
        try {
            var existing = window.sessionStorage.getItem(LOCAL_DRAFT_TAB_ID_STORAGE_KEY);
            if (existing) return existing;

            var created = createLocalDraftTabId();
            window.sessionStorage.setItem(LOCAL_DRAFT_TAB_ID_STORAGE_KEY, created);
            return created;
        } catch (_) {
            // sessionStorage blocked: isolate at least within this page instance.
            if (!localDraftMemoryTabId) {
                localDraftMemoryTabId = createLocalDraftTabId();
            }
            return localDraftMemoryTabId;
        }
    }

    function buildLocalDraftKey(baseKey) {
        var base = (baseKey || '').trim();
        if (!base) return '';
        return base + '.' + getLocalDraftTabId();
    }

    function removeLocalDraftKey(key) {
        if (!key) return false;
        try {
            window.localStorage.removeItem(key);
            return true;
        } catch (_) {
            return false;
        }
    }

    function readLocalDraftData(key) {
        try {
            var raw = window.localStorage.getItem(key);
            if (!raw) return null;

            var data = JSON.parse(raw);
            if (!data || !data.savedAt) return null;
            return data;
        } catch (_) {
            return null;
        }
    }

    function localDraftHeartbeatKey(tabId) {
        return LOCAL_DRAFT_HEARTBEAT_PREFIX + (tabId || '');
    }

    function isLocalDraftTabAlive(tabId) {
        if (!tabId) return false;
        try {
            var raw = window.localStorage.getItem(localDraftHeartbeatKey(tabId));
            if (!raw) return false;

            var last = parseInt(raw, 10);
            return Number.isFinite(last) && (Date.now() - last) < LOCAL_DRAFT_HEARTBEAT_TTL_MS;
        } catch (_) {
            return false;
        }
    }

    // Removes a draft and the heartbeat of the tab that owned it.
    function removeLocalDraftEntry(baseKey, key) {
        removeLocalDraftKey(key);

        var base = (baseKey || '').trim();
        if (!base || !key || key.indexOf(base + '.') !== 0) return;

        removeLocalDraftKey(localDraftHeartbeatKey(key.substring(base.length + 1)));
    }

    function startLocalDraftHeartbeat() {
        if (localDraftHeartbeatStarted) return;
        localDraftHeartbeatStarted = true;

        var tabId = getLocalDraftTabId();
        var heartbeatKey = localDraftHeartbeatKey(tabId);
        var timer = null;

        function beat() {
            try {
                window.localStorage.setItem(heartbeatKey, String(Date.now()));
            } catch (_) { }
        }

        // Heartbeat entries of crashed tabs are never removed by pagehide; drop the
        // ones that are already past the alive window so they cannot pile up.
        function cleanupStaleHeartbeats() {
            var stale = [];
            try {
                for (var i = 0; i < window.localStorage.length; i++) {
                    var key = window.localStorage.key(i);
                    if (!key || key.indexOf(LOCAL_DRAFT_HEARTBEAT_PREFIX) !== 0 || key === heartbeatKey) continue;

                    var last = parseInt(window.localStorage.getItem(key), 10);
                    if (!Number.isFinite(last) || (Date.now() - last) >= LOCAL_DRAFT_HEARTBEAT_TTL_MS) {
                        stale.push(key);
                    }
                }
            } catch (_) {
                return;
            }

            stale.forEach(removeLocalDraftKey);
        }

        function startTimer() {
            if (timer) window.clearInterval(timer);
            timer = window.setInterval(beat, LOCAL_DRAFT_HEARTBEAT_INTERVAL_MS);
        }

        cleanupStaleHeartbeats();
        beat();
        startTimer();

        // A closed tab stops being "alive" immediately, so its draft can be offered
        // for recovery in a new tab; a crashed tab is detected after the TTL.
        window.addEventListener('pagehide', function () {
            if (timer) {
                window.clearInterval(timer);
                timer = null;
            }
            removeLocalDraftKey(heartbeatKey);
        });

        window.addEventListener('pageshow', function (event) {
            if (event && event.persisted) {
                beat();
                startTimer();
            }
        });

        document.addEventListener('visibilitychange', function () {
            if (!document.hidden) beat();
        });
    }

    // Returns the most recent draft that belongs to no live tab (closed/crashed tab),
    // skipping the current tab's own draft. Expired/corrupt entries are cleaned up.
    function findLocalDraftOrphan(baseKey, ownKey, ttlMinutes) {
        var base = (baseKey || '').trim();
        if (!base) return null;

        var prefix = base + '.';
        var minutes = ttlMinutes > 0 ? ttlMinutes : 720;
        var ttlMs = minutes * 60 * 1000;
        var now = Date.now();
        var best = null;
        var staleKeys = [];

        try {
            for (var i = 0; i < window.localStorage.length; i++) {
                var key = window.localStorage.key(i);
                if (!key || key.indexOf(prefix) !== 0 || key === ownKey) continue;

                var data = readLocalDraftData(key);
                if (!data || (now - data.savedAt) > ttlMs) {
                    staleKeys.push(key);
                    continue;
                }

                var tabId = key.substring(base.length + 1);
                if (isLocalDraftTabAlive(tabId)) continue;

                if (!best || data.savedAt > best.data.savedAt) {
                    best = { key: key, data: data };
                }
            }
        } catch (_) {
            return null;
        }

        staleKeys.forEach(function (key) {
            removeLocalDraftEntry(base, key);
        });

        return best;
    }

    window.PosLocalDraft = {
        buildKey: buildLocalDraftKey,
        removeKey: removeLocalDraftKey,
        remove: function (baseKey) {
            var base = (baseKey || '').trim();
            if (!base) return;

            removeLocalDraftEntry(base, buildLocalDraftKey(base));
            // Legacy single-key draft shared by every tab (pre per-tab keys).
            removeLocalDraftKey(base);
        },
        startHeartbeat: startLocalDraftHeartbeat,
        isTabAlive: isLocalDraftTabAlive,
        findOrphan: findLocalDraftOrphan,
        removeEntry: removeLocalDraftEntry
    };

    function parseDecimal(value) {
        var number = parseFloat(value);
        return Number.isFinite(number) ? number : 0;
    }

    function fixed2(value) {
        return parseDecimal(value).toFixed(2);
    }

    function isEffectivelyEmptyNumericInput(rawValue) {
        if (rawValue === null || typeof rawValue === 'undefined') return true;
        var text = String(rawValue).trim();
        if (!text) return true;
        var numeric = parseDecimal(text);
        return numeric === 0;
    }

    function getToken(form) {
        var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    function setSelectValue(select, value) {
        if (!select) return;
        if (select.tomselect) {
            select.tomselect.setValue(value || '');
        } else {
            select.value = value || '';
            select.dispatchEvent(new Event('change', { bubbles: true }));
        }
    }

    function initInvoiceForm(form) {
        if (!form || form.dataset.invoiceLinesBound === '1') return;
        var tableBody = form.querySelector('#lines-table tbody');
        var lineTemplate = form.querySelector('#line-template') || document.getElementById('line-template');
        if (!tableBody || !lineTemplate) return;
        form.dataset.invoiceLinesBound = '1';

        var addButton = form.querySelector('#add-line');
        var rateInput = form.querySelector('#EurToDinarRateSnapshot');
        var totalEurEl = form.querySelector('#live-total-eur');
        var discountEurEl = form.querySelector('#live-discount-eur');
        var netEurEl = form.querySelector('#live-net-eur');
        var totalSecondaryEl = form.querySelector('#live-total-secondary');
        var discountTypeInput = form.querySelector('#discount-type');
        var discountValueInput = form.querySelector('#discount-value');
        var isReturnToggle = form.querySelector('#is-return-toggle');
        var submitPosBtn = form.querySelector('#submit-pos-btn');
        var holdInvoiceBtn = form.querySelector('#hold-invoice-btn');
        var restoreInvoiceBtn = form.querySelector('#restore-invoice-btn');
        var draftIdInput = form.querySelector('input[name="DraftId"]');
        var autoSaveTimer = null;
        var autoSaveDirty = false;
        var autoSaveInFlight = false;
        var autoSaveDelayMs = 45000;
        var localDraftBaseKey = (form.dataset.localDraftBaseKey || form.dataset.localDraftKey || '').trim();
        var localDraftKey = localDraftBaseKey ? window.PosLocalDraft.buildKey(localDraftBaseKey) : '';
        var localDraftLastPostedAt = parseInt(form.dataset.localDraftLastPostedAt || '0', 10);
        if (!Number.isFinite(localDraftLastPostedAt) || localDraftLastPostedAt < 0) {
            localDraftLastPostedAt = 0;
        }
        var localDraftTtlMinutes = parseInt(form.dataset.localDraftTtlMinutes || '720', 10);
        if (!Number.isFinite(localDraftTtlMinutes) || localDraftTtlMinutes <= 0) {
            localDraftTtlMinutes = 720;
        }
        var localDraftEnabled = !!localDraftKey;
        var isEditMode = form.dataset.editMode === 'true';
        var localDraftBanner = document.getElementById('local-draft-banner');
        var localDraftBannerTitle = document.getElementById('local-draft-banner-title');
        var localDraftBannerTime = document.getElementById('local-draft-banner-time');
        var localDraftDiscardBtn = document.getElementById('local-draft-discard-btn');
        var localDraftRecoverBtn = document.getElementById('local-draft-recover-btn');
        var pendingOrphanDraftKey = '';
        var restoringLocalDraft = false;

        var barcodeInput = form.querySelector('#barcode-input');
        var barcodeAddBtn = form.querySelector('#barcode-add-btn');
        var barcodeCameraBtn = form.querySelector('#barcode-camera-btn');
        var barcodeUndoBtn = form.querySelector('#barcode-undo-btn');
        var barcodeSoundToggle = form.querySelector('#barcode-sound-toggle');
        var barcodeWarning = form.querySelector('#barcode-warning');
        var barcodeLastAdded = form.querySelector('#barcode-last-added');
        var barcodeLastAddedText = form.querySelector('#barcode-last-added-text');
        var barcodeUnknownWrapper = form.querySelector('#barcode-unknown-wrapper');
        var barcodeUnknownMessage = form.querySelector('#barcode-unknown-message');
        var barcodeCreateLink = form.querySelector('#barcode-create-item-link');
        var barcodeMapItemSelect = form.querySelector('#barcode-map-item-select');
        var barcodeMapExistingBtn = form.querySelector('#barcode-map-existing-btn');

        function focusBarcodeInput() {
            if (!barcodeInput) return;
            barcodeInput.focus();
            barcodeInput.select();
        }

        var cameraModalEl = form.querySelector('#barcode-camera-modal') || document.getElementById('barcode-camera-modal');
        var barcodeVideo = form.querySelector('#barcode-video') || document.getElementById('barcode-video');
        var cameraModal = cameraModalEl ? new bootstrap.Modal(cameraModalEl) : null;

        var reader = null;
        var scanning = false;
        var lastScanText = '';
        var lastScanAt = 0;

        var resolveUrl = form.dataset.resolveUrl || '';
        var mapUrl = form.dataset.mapUrl || '';
        var createItemUrl = form.dataset.createItemUrl || '';
        var holdUrl = form.dataset.holdUrl || '';
        var newUrl = form.dataset.newUrl || window.location.pathname;
        var canCreateItem = (form.dataset.canCreateItem || '').toLowerCase() === 'true';
        var pendingMappingPayload = null;
        var barcodeLastAddedTimer = null;
        var audioContext = null;
        var scanHistory = [];
        var barcodeQueue = [];
        var barcodeResolving = false;
        var soundUserKey = ((form.dataset.soundUserKey || 'anonymous') + '').trim().toLowerCase();
        var soundStorageKey = 'pos.sound.enabled.' + soundUserKey;
        var soundEnabled = true;

        var textPostSale = form.dataset.textPostSale || 'Post Sale';
        var textPostReturn = form.dataset.textPostReturn || 'Post Return';
        var msgBarcodeMappingUnavailable = form.dataset.msgBarcodeMappingUnavailable || 'Barcode mapping is not available.';
        var msgSelectExistingItemFirst = form.dataset.msgSelectExistingItemFirst || 'Select an existing item first.';
        var msgUnableMapBarcode = form.dataset.msgUnableMapBarcode || 'Unable to map barcode to selected item.';
        var msgHoldServiceUnavailable = form.dataset.msgHoldServiceUnavailable || 'Hold service is not configured.';
        var msgUnableHoldInvoice = form.dataset.msgUnableHoldInvoice || 'Unable to hold invoice.';
        var msgBarcodeServiceUnavailable = form.dataset.msgBarcodeServiceUnavailable || 'Barcode service is not configured.';
        var msgUnableResolveBarcode = form.dataset.msgUnableResolveBarcode || 'Unable to resolve barcode.';
        var msgBarcodeNotFound = form.dataset.msgBarcodeNotFound || 'Barcode not found.';
        var msgCameraUnavailable = form.dataset.msgCameraUnavailable || 'Camera scanner is not available.';
        var msgScanOrTypeFirst = form.dataset.msgScanOrTypeFirst || 'Scan or type a barcode first.';
        var msgUnknownWithCreate = form.dataset.msgUnknownWithCreate || 'Barcode {code} is not linked to an item. You can map it to an existing item or create a new item.';
        var msgUnknownWithoutCreate = form.dataset.msgUnknownWithoutCreate || 'Barcode {code} is not linked to an item. You can map it to an existing item.';
        var createCustomerUrl = form.dataset.createCustomerUrl || '';
        var msgCustomerNameRequired = form.dataset.msgCustomerNameRequired || 'Customer name is required.';
        var msgUnableCreateCustomer = form.dataset.msgUnableCreateCustomer || 'Unable to create customer.';
        var simplePosMode = (form.dataset.simplePosMode || '').toLowerCase() === 'true';
        var itemDefaultPrices = {};
        var itemSalePrices = {};

        function normalizePriceKeys(source) {
            var result = {};
            if (!source || typeof source !== 'object') return result;
            Object.keys(source).forEach(function (key) {
                result[String(key).toLowerCase()] = source[key];
            });
            return result;
        }

        function readSoundSetting() {
            try {
                var saved = window.localStorage.getItem(soundStorageKey);
                if (saved === null || typeof saved === 'undefined') return true;
                return saved === '1' || saved === 'true';
            } catch (_) {
                return true;
            }
        }

        function writeSoundSetting(enabled) {
            try {
                window.localStorage.setItem(soundStorageKey, enabled ? '1' : '0');
            } catch (_) {
                // Ignore storage errors (private mode / blocked storage).
            }
        }

        try {
            var jsonScript = document.getElementById('item-default-prices-json');
            if (jsonScript && jsonScript.textContent) {
                itemDefaultPrices = normalizePriceKeys(JSON.parse(jsonScript.textContent));
            } else {
                itemDefaultPrices = normalizePriceKeys(JSON.parse(form.dataset.itemDefaultPrices || '{}'));
            }
        } catch (_) {
            try {
                itemDefaultPrices = normalizePriceKeys(JSON.parse(form.dataset.itemDefaultPrices || '{}'));
            } catch (__)
            {
                itemDefaultPrices = {};
            }
        }

        try {
            var salePricesScript = document.getElementById('item-sale-prices-json');
            if (salePricesScript && salePricesScript.textContent) {
                itemSalePrices = normalizePriceKeys(JSON.parse(salePricesScript.textContent));
            } else {
                itemSalePrices = normalizePriceKeys(JSON.parse(form.dataset.itemSalePrices || '{}'));
            }
        } catch (_) {
            try {
                itemSalePrices = normalizePriceKeys(JSON.parse(form.dataset.itemSalePrices || '{}'));
            } catch (__)
            {
                itemSalePrices = {};
            }
        }

        var customerSelect = form.querySelector('#CustomerId');
        var customerSection = form.querySelector('#customer-section');
        var bankSection = form.querySelector('#bank-section');
        var bankSelect = form.querySelector('#BankId');
        var customerRequiredHint = form.querySelector('#customer-required-hint');
        var invoiceDateInput = form.querySelector('#InvoiceDate');
        var paymentMethodSelect = form.querySelector('#payment-method');
        var supplierSelect = form.querySelector('#supplier-id');
        var dueDateInput = form.querySelector('#DueDate');
        var noteInput = form.querySelector('#Note');
        var paymentMethodInputs = form.querySelectorAll('input[name="PaymentMethod"]');
        var quickCustomerModalEl = document.getElementById('quick-customer-modal');
        var quickCustomerModal = quickCustomerModalEl ? new bootstrap.Modal(quickCustomerModalEl) : null;
        var quickCustomerNameInput = document.getElementById('quick-customer-name');
        var quickCustomerPhoneInput = document.getElementById('quick-customer-phone');
        var quickCustomerSaveBtn = document.getElementById('quick-customer-save');
        var quickCustomerError = document.getElementById('quick-customer-error');

        function hideWarning() {
            if (!barcodeWarning) return;
            barcodeWarning.classList.add('d-none');
            barcodeWarning.textContent = '';
        }

        function hideLastAdded() {
            if (!barcodeLastAdded) return;
            barcodeLastAdded.classList.add('d-none');
            if (barcodeLastAddedText) {
                barcodeLastAddedText.textContent = '';
            }
        }

        function showLastAdded(itemName, qtyAdded) {
            if (!barcodeLastAdded || !barcodeLastAddedText) return;
            if (barcodeLastAddedTimer) {
                window.clearTimeout(barcodeLastAddedTimer);
            }

            var safeName = (itemName || '').trim() || 'صنف';
            barcodeLastAddedText.textContent = safeName + ' (+' + qtyAdded + ')';
            barcodeLastAdded.classList.remove('d-none');

            barcodeLastAddedTimer = window.setTimeout(function () {
                hideLastAdded();
            }, 2200);
        }

        function recordScanHistory(entry) {
            scanHistory.push(entry);
            if (scanHistory.length > 100) {
                scanHistory.shift();
            }
        }

        function undoLastScan() {
            if (!scanHistory.length) {
                showWarning('لا يوجد مسح سابق للتراجع عنه.');
                focusBarcodeInput();
                return;
            }

            var entry = scanHistory.pop();
            var row = entry ? entry.row : null;
            if (!row || !tableBody.contains(row)) {
                showWarning('تعذر التراجع، تم تغيير السطر سابقا.');
                focusBarcodeInput();
                return;
            }

            var itemSelect = row.querySelector('.item-select');
            var qtyInput = row.querySelector('.qty-input');
            var priceInput = row.querySelector('.price-input');
            var lineTotalInput = row.querySelector('.line-total');

            if (entry.prevQty <= 0) {
                var rowsCount = tableBody.querySelectorAll('tr').length;
                if (rowsCount > 1) {
                    row.remove();
                    reindexLines();
                } else {
                    setSelectValue(itemSelect, '');
                    if (qtyInput) qtyInput.value = '';
                    if (priceInput) priceInput.value = '';
                    if (lineTotalInput) lineTotalInput.value = '0.00';
                }
            } else {
                if (itemSelect) setSelectValue(itemSelect, entry.itemId || '');
                if (qtyInput) {
                    qtyInput.value = String(entry.prevQty);
                    qtyInput.dispatchEvent(new Event('input', { bubbles: true }));
                }
            }

            calculateTotals();
            hideWarning();
            hideUnknown();
            showLastAdded('تم التراجع عن ' + (entry.itemName || 'الصنف'), 1);
            playTone('success');
            focusBarcodeInput();
        }

        function ensureAudioContext() {
            if (audioContext) return audioContext;
            var Ctx = window.AudioContext || window.webkitAudioContext;
            if (!Ctx) return null;
            audioContext = new Ctx();
            return audioContext;
        }

        function playTone(type) {
            if (!soundEnabled) return;

            var ctx = ensureAudioContext();
            if (!ctx) return;

            if (ctx.state === 'suspended') {
                ctx.resume().catch(function () { });
            }

            var oscillator = ctx.createOscillator();
            var gainNode = ctx.createGain();
            oscillator.connect(gainNode);
            gainNode.connect(ctx.destination);

            var now = ctx.currentTime;
            var isSuccess = type === 'success';

            oscillator.type = isSuccess ? 'triangle' : 'square';
            oscillator.frequency.setValueAtTime(isSuccess ? 1120 : 240, now);
            if (isSuccess) {
                oscillator.frequency.exponentialRampToValueAtTime(1320, now + 0.08);
            } else {
                oscillator.frequency.exponentialRampToValueAtTime(180, now + 0.12);
            }

            gainNode.gain.setValueAtTime(0.0001, now);
            gainNode.gain.exponentialRampToValueAtTime(isSuccess ? 0.05 : 0.06, now + 0.02);
            gainNode.gain.exponentialRampToValueAtTime(0.0001, now + (isSuccess ? 0.11 : 0.14));

            oscillator.start(now);
            oscillator.stop(now + (isSuccess ? 0.12 : 0.15));
        }

        function clearAndRefocusBarcodeInput() {
            if (!barcodeInput) return;
            barcodeInput.value = '';
            focusBarcodeInput();
        }

        function shouldKeepBarcodeFocus(target) {
            if (!target) return true;
            return !target.closest('input, textarea, select, [contenteditable="true"], .ts-dropdown, .tomselect, .modal');
        }

        function installBarcodeFirstMode() {
            if (!simplePosMode || !barcodeInput) return;

            document.addEventListener('click', function (event) {
                if (shouldKeepBarcodeFocus(event.target)) {
                    focusBarcodeInput();
                }
            });

            window.addEventListener('focus', focusBarcodeInput);

            document.addEventListener('visibilitychange', function () {
                if (!document.hidden) {
                    focusBarcodeInput();
                }
            });

            document.addEventListener('keydown', function (event) {
                if (event.key === 'F2') {
                    event.preventDefault();
                    focusBarcodeInput();
                    return;
                }

                if (event.ctrlKey && (event.key === 'z' || event.key === 'Z')) {
                    var active = document.activeElement;
                    var isTypingField = active && active.matches && active.matches('textarea, input[type="text"], input[type="search"], input[type="email"], input[type="password"]');
                    if (!isTypingField || active === barcodeInput) {
                        event.preventDefault();
                        undoLastScan();
                    }
                }
            });
        }

        function initializeSoundToggle() {
            soundEnabled = readSoundSetting();
            if (!barcodeSoundToggle) return;

            barcodeSoundToggle.checked = soundEnabled;
            barcodeSoundToggle.addEventListener('change', function () {
                soundEnabled = !!barcodeSoundToggle.checked;
                writeSoundSetting(soundEnabled);
                focusBarcodeInput();
            });
        }

        function showWarning(message) {
            if (!barcodeWarning) return;
            barcodeWarning.textContent = message;
            barcodeWarning.classList.remove('d-none');
            playTone('error');
        }

        function showQuickCustomerError(message) {
            if (!quickCustomerError) return;
            quickCustomerError.textContent = message;
            quickCustomerError.classList.remove('d-none');
        }

        function hideQuickCustomerError() {
            if (!quickCustomerError) return;
            quickCustomerError.textContent = '';
            quickCustomerError.classList.add('d-none');
        }

        function syncCustomerRequirement() {
            if (!customerSelect) return;

            var selectedPayment = form.querySelector('input[name="PaymentMethod"]:checked');
            var isCredit = !!selectedPayment && selectedPayment.value === 'Credit';
            var isTransfer = !!selectedPayment && selectedPayment.value === 'Transfer';
            customerSelect.required = isCredit;
            if (bankSelect) bankSelect.required = isTransfer;

            if (customerSection) {
                customerSection.classList.toggle('d-none', !isCredit);
            }
            if (bankSection) {
                bankSection.classList.toggle('d-none', !isTransfer);
            }

            if (!isCredit) {
                setSelectValue(customerSelect, '');
            }
            if (!isTransfer && bankSelect) {
                setSelectValue(bankSelect, '');
            }

            if (customerRequiredHint) {
                customerRequiredHint.classList.toggle('text-danger', isCredit);
            }
        }

        function hideUnknown() {
            pendingMappingPayload = null;
            if (!barcodeUnknownWrapper) return;
            barcodeUnknownWrapper.classList.add('d-none');
            if (barcodeMapItemSelect) {
                barcodeMapItemSelect.value = '';
            }
        }

        function showUnknown(payload) {
            if (!barcodeUnknownWrapper || !barcodeUnknownMessage) return;

            pendingMappingPayload = payload || null;
            hideLastAdded();
            var code = payload && payload.code ? payload.code : '';

            var template = canCreateItem ? msgUnknownWithCreate : msgUnknownWithoutCreate;
            barcodeUnknownMessage.textContent = template.replace('{code}', code);

            if (barcodeCreateLink) {
                if (canCreateItem) {
                    try {
                        var url = new URL(createItemUrl, window.location.origin);
                        url.searchParams.set('barcode', code);
                        barcodeCreateLink.href = url.pathname + url.search;
                    } catch (_) {
                        barcodeCreateLink.href = createItemUrl;
                    }
                    barcodeCreateLink.classList.remove('d-none');
                } else {
                    barcodeCreateLink.classList.add('d-none');
                }
            }

            barcodeUnknownWrapper.classList.remove('d-none');
        }

        function applyDefaultPriceForRow(row, forceOverride) {
            if (!row) return;

            var itemSelect = row.querySelector('.item-select');
            var priceInput = row.querySelector('.price-input');
            if (!itemSelect || !priceInput) return;

            priceInput.readOnly = simplePosMode;

            var selectedItemId = (itemSelect.value || '').trim().toLowerCase();
            if (!selectedItemId) {
                if (forceOverride) {
                    priceInput.value = '';
                    priceInput.dispatchEvent(new Event('input', { bubbles: true }));
                }
                return;
            }

            var mapped = itemDefaultPrices[selectedItemId];
            var defaultPrice = parseDecimal(mapped);
            if (forceOverride || isEffectivelyEmptyNumericInput(priceInput.value)) {
                priceInput.value = defaultPrice > 0 ? fixed2(defaultPrice) : '';
                priceInput.dispatchEvent(new Event('input', { bubbles: true }));
            }
        }

        function applyDefaultSellPriceForRow(row, forceOverride) {
            if (!row) return;

            var itemSelect = row.querySelector('.item-select');
            var sellPriceInput = row.querySelector('.sell-price-input');
            if (!itemSelect || !sellPriceInput) return;

            var selectedItemId = (itemSelect.value || '').trim().toLowerCase();
            if (!selectedItemId) {
                if (forceOverride) {
                    sellPriceInput.value = '';
                    sellPriceInput.dispatchEvent(new Event('input', { bubbles: true }));
                }
                return;
            }

            var mapped = itemSalePrices[selectedItemId];
            var defaultPrice = parseDecimal(mapped);
            if (forceOverride || isEffectivelyEmptyNumericInput(sellPriceInput.value)) {
                sellPriceInput.value = defaultPrice > 0 ? fixed2(defaultPrice) : '';
                sellPriceInput.dispatchEvent(new Event('input', { bubbles: true }));
            }
        }

        async function mapUnknownBarcodeToExistingItem() {
            if (!pendingMappingPayload || !mapUrl) {
                showWarning(msgBarcodeMappingUnavailable);
                return;
            }

            var itemId = barcodeMapItemSelect ? (barcodeMapItemSelect.value || '').trim() : '';
            if (!itemId) {
                showWarning(msgSelectExistingItemFirst);
                return;
            }

            var response = await fetch(mapUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getToken(form)
                },
                body: JSON.stringify({
                    code: pendingMappingPayload.code,
                    codeType: pendingMappingPayload.codeType || 'Raw',
                    itemId: itemId
                })
            });

            if (!response.ok) {
                showWarning(msgUnableMapBarcode);
                return;
            }

            var data = await response.json();
            if (!data.success) {
                showWarning(data.error || msgUnableMapBarcode);
                return;
            }

            hideUnknown();
            hideWarning();
            addItemById(itemId);
        }

        function reindexLines() {
            var rows = tableBody.querySelectorAll('tr');
            rows.forEach(function (row, index) {
                row.querySelectorAll('input[name], select[name], textarea[name]').forEach(function (field) {
                    field.name = field.name.replace(/Lines\[(\d+|__index__)\]/g, 'Lines[' + index + ']');
                });
                row.querySelectorAll('span[data-valmsg-for]').forEach(function (span) {
                    span.setAttribute('data-valmsg-for', span.getAttribute('data-valmsg-for').replace(/Lines\[(\d+|__index__)\]/g, 'Lines[' + index + ']'));
                });
            });
        }

        function calculateTotals() {
            var rows = tableBody.querySelectorAll('tr');
            var subtotalEur = 0;

            rows.forEach(function (row) {
                var qty = parseDecimal((row.querySelector('.qty-input') || {}).value);
                var unitPrice = parseDecimal((row.querySelector('.price-input') || {}).value);
                var lineTotal = qty * unitPrice;
                subtotalEur += lineTotal;

                var lineTotalInput = row.querySelector('.line-total');
                if (lineTotalInput) {
                    lineTotalInput.value = fixed2(lineTotal);
                }
            });

            subtotalEur = parseDecimal(fixed2(subtotalEur));

            var discountValue = parseDecimal(discountValueInput ? discountValueInput.value : 0);
            if (discountValue < 0) discountValue = 0;

            var discountType = (discountTypeInput ? discountTypeInput.value : 'Amount') || 'Amount';
            var discountEur = discountType === 'Percent'
                ? subtotalEur * (discountValue / 100)
                : discountValue;

            if (discountEur > subtotalEur) {
                discountEur = subtotalEur;
            }

            var netEur = subtotalEur - discountEur;

            var rate = parseDecimal(rateInput ? rateInput.value : 0);
            var totalSecondary = netEur * rate;

            if (totalEurEl) totalEurEl.textContent = fixed2(subtotalEur);
            if (discountEurEl) discountEurEl.textContent = fixed2(discountEur);
            if (netEurEl) netEurEl.textContent = fixed2(netEur);
            if (totalSecondaryEl) totalSecondaryEl.textContent = fixed2(totalSecondary);
        }

        function applyReturnUiState() {
            if (!isReturnToggle || !submitPosBtn) return;
            // While editing a posted invoice the action is always "save changes";
            // the return toggle must not relabel it as a new sale/return.
            if (isEditMode) return;
            var isReturn = !!isReturnToggle.checked;
            submitPosBtn.textContent = isReturn ? textPostReturn : textPostSale;
            submitPosBtn.classList.toggle('btn-warning', isReturn);
            submitPosBtn.classList.toggle('btn-primary', !isReturn);
        }

        // Guard against accidental double-submit of the sale/return (avoids duplicate
        // invoices on slow/touch POS) and show visible "saving…" feedback.
        var posSubmitting = false;
        form.addEventListener('submit', function (e) {
            if (typeof form.checkValidity === 'function' && !form.checkValidity()) {
                return; // let validation surface errors; keep the button usable
            }
            if (posSubmitting) { e.preventDefault(); return; }

            // Persist the latest edits right before the request leaves the page, so a
            // session timeout or dropped connection does not lose the last seconds of
            // work (the server clears this draft once the invoice is posted).
            if (!holdUrl && localDraftEnabled) {
                if (saveLocalDraft()) {
                    autoSaveDirty = false;
                }
            }

            posSubmitting = true;
            if (submitPosBtn) {
                if (!submitPosBtn.dataset.originalHtml) {
                    submitPosBtn.dataset.originalHtml = submitPosBtn.innerHTML;
                }
                submitPosBtn.disabled = true;
                submitPosBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>'
                    + (submitPosBtn.dataset.savingText || (isEditMode ? 'جارٍ الحفظ…' : 'جارٍ الترحيل…'));
            }
            // Safety net: if we're still on the page later (e.g. server-side validation
            // returned the form), re-enable so the cashier is never locked out.
            setTimeout(function () {
                posSubmitting = false;
                if (submitPosBtn) {
                    submitPosBtn.disabled = false;
                    if (submitPosBtn.dataset.originalHtml) {
                        submitPosBtn.innerHTML = submitPosBtn.dataset.originalHtml;
                    }
                }
            }, 12000);
        });

        function resetToNewSale() {
            window.location.href = newUrl || window.location.pathname;
        }

        function hasDraftableContent() {
            var rows = tableBody.querySelectorAll('tr');
            for (var i = 0; i < rows.length; i++) {
                var itemSelect = rows[i].querySelector('.item-select');
                var qtyInput = rows[i].querySelector('.qty-input');
                var priceInput = rows[i].querySelector('.price-input');
                var sellPriceInput = rows[i].querySelector('.sell-price-input');

                if (itemSelect && itemSelect.value) return true;
                if (qtyInput && qtyInput.value) return true;
                if (priceInput && priceInput.value) return true;
                if (sellPriceInput && sellPriceInput.value) return true;
            }

            return !!(draftIdInput && draftIdInput.value);
        }

        function hasLocalDraftContent() {
            if (hasDraftableContent()) return true;
            if (supplierSelect && supplierSelect.value) return true;
            if (customerSelect && customerSelect.value) return true;
            if (bankSelect && bankSelect.value) return true;
            if (dueDateInput && dueDateInput.value) return true;
            if (discountValueInput && discountValueInput.value && parseDecimal(discountValueInput.value) !== 0) return true;
            if (noteInput && noteInput.value && noteInput.value.trim()) return true;
            return false;
        }

        function isFormEmptyForRestore() {
            if (hasLocalDraftContent()) return false;
            return true;
        }

        function buildLocalDraftPayload() {
            var rows = tableBody.querySelectorAll('tr');
            var lines = [];
            rows.forEach(function (row) {
                var itemSelect = row.querySelector('.item-select');
                var qtyInput = row.querySelector('.qty-input');
                var priceInput = row.querySelector('.price-input');
                var sellPriceInput = row.querySelector('.sell-price-input');
                lines.push({
                    itemId: itemSelect ? itemSelect.value : '',
                    qty: qtyInput ? qtyInput.value : '',
                    unitPriceEur: priceInput ? priceInput.value : '',
                    sellPriceLyd: sellPriceInput ? sellPriceInput.value : ''
                });
            });

            return {
                savedAt: Date.now(),
                invoiceDate: invoiceDateInput ? invoiceDateInput.value : '',
                paymentMethod: paymentMethodSelect ? paymentMethodSelect.value : '',
                supplierId: supplierSelect ? supplierSelect.value : '',
                customerId: customerSelect ? customerSelect.value : '',
                bankId: bankSelect ? bankSelect.value : '',
                dueDate: dueDateInput ? dueDateInput.value : '',
                discountType: discountTypeInput ? discountTypeInput.value : '',
                discountValue: discountValueInput ? discountValueInput.value : '',
                note: noteInput ? noteInput.value : '',
                lines: lines
            };
        }

        function saveLocalDraft() {
            if (!localDraftEnabled) return false;
            try {
                var payload = buildLocalDraftPayload();
                window.localStorage.setItem(localDraftKey, JSON.stringify(payload));
                pendingOrphanDraftKey = '';
                showLocalDraftBanner(payload.savedAt, 'saved');
                return true;
            } catch (_) {
                return false;
            }
        }

        function formatLocalDraftTime(timestamp) {
            try {
                var date = new Date(timestamp);
                if (isNaN(date.getTime())) return '';

                function pad(value) {
                    return value < 10 ? '0' + value : '' + value;
                }

                return pad(date.getDate()) + '/' + pad(date.getMonth() + 1) + '/' + date.getFullYear()
                    + ' - ' + pad(date.getHours()) + ':' + pad(date.getMinutes());
            } catch (_) {
                return '';
            }
        }

        function showLocalDraftBanner(savedAt, state) {
            if (!localDraftBanner) return;

            localDraftBanner.classList.remove('d-none');
            if (localDraftBannerTitle) {
                if (state === 'restored') {
                    localDraftBannerTitle.textContent = 'تم استرجاع مسودة محفوظة محلياً';
                } else if (state === 'recovered') {
                    localDraftBannerTitle.textContent = 'تم استرجاع مسودة من جلسة سابقة';
                } else if (state === 'saved') {
                    localDraftBannerTitle.textContent = 'تم تحديث النسخة الاحتياطية محلياً — الفاتورة لم تُرحَّل بعد';
                } else if (state === 'orphan') {
                    localDraftBannerTitle.textContent = 'توجد مسودة غير مكتملة من جلسة سابقة';
                } else {
                    localDraftBannerTitle.textContent = 'توجد نسخة احتياطية محلية — الفاتورة لم تُرحَّل بعد';
                }
            }
            if (localDraftBannerTime) {
                var formatted = formatLocalDraftTime(savedAt);
                localDraftBannerTime.textContent = formatted ? 'آخر تحديث: ' + formatted : '';
            }
            if (localDraftRecoverBtn) {
                localDraftRecoverBtn.classList.toggle('d-none', state !== 'orphan');
            }
        }

        function hideLocalDraftBanner() {
            if (!localDraftBanner) return;
            localDraftBanner.classList.add('d-none');
        }

        function discardLocalDraft() {
            if (!localDraftEnabled) return;

            if (pendingOrphanDraftKey) {
                if (!window.confirm('حذف المسودة غير المكتملة من الجلسة السابقة نهائياً؟')) {
                    return;
                }

                window.PosLocalDraft.removeEntry(localDraftBaseKey, pendingOrphanDraftKey);
                pendingOrphanDraftKey = '';
                hideLocalDraftBanner();
                return;
            }

            if (!window.confirm('هل تريد حذف المسودة المحفوظة؟ سيتم تفريغ الشاشة. لن تتأثر الفواتير المرحّلة.')) {
                return;
            }

            window.PosLocalDraft.remove(localDraftBaseKey);

            if (autoSaveTimer) {
                window.clearTimeout(autoSaveTimer);
                autoSaveTimer = null;
            }
            autoSaveDirty = false;
            hideLocalDraftBanner();

            // Reload the empty create screen so the restored data does not stay on display.
            window.location.href = newUrl || window.location.pathname;
        }

        function recoverLocalDraft() {
            if (!pendingOrphanDraftKey) return;

            var data = readLocalDraftData(pendingOrphanDraftKey);
            if (!data) {
                pendingOrphanDraftKey = '';
                hideLocalDraftBanner();
                return;
            }

            if (!isFormEmptyForRestore()
                && !window.confirm('سيتم استبدال البيانات المكتوبة حالياً بمسودة الجلسة السابقة. هل تريد المتابعة؟')) {
                return;
            }

            // Adopt the orphan payload as this tab's own draft, then drop the source.
            // The TTL restarts now because the user explicitly chose to keep it, even
            // if the data itself was typed close to the expiry limit.
            var adopted = {};
            for (var prop in data) {
                if (Object.prototype.hasOwnProperty.call(data, prop)) {
                    adopted[prop] = data[prop];
                }
            }
            adopted.savedAt = Date.now();

            try {
                window.localStorage.setItem(localDraftKey, JSON.stringify(adopted));
            } catch (_) {
                return;
            }

            window.PosLocalDraft.removeEntry(localDraftBaseKey, pendingOrphanDraftKey);
            pendingOrphanDraftKey = '';

            applyLocalDraftData(data);
            showLocalDraftBanner(data.savedAt, 'recovered');
            calculateTotals();
        }

        // Older versions used one shared key per user. Move a still-useful draft
        // into this tab's key, but drop it when it was already posted (its savedAt
        // predates the latest posted purchase invoice) or when it expired.
        function migrateLegacyLocalDraft() {
            var legacyRaw = null;
            try {
                legacyRaw = window.localStorage.getItem(localDraftBaseKey);
            } catch (_) {
                return;
            }
            if (!legacyRaw) return;

            var data = readLocalDraftData(localDraftBaseKey);
            var ttlMs = localDraftTtlMinutes * 60 * 1000;
            var expired = !data || (ttlMs > 0 && (Date.now() - data.savedAt) > ttlMs);
            var alreadyPosted = !!data
                && localDraftLastPostedAt > 0
                && data.savedAt <= localDraftLastPostedAt;

            if (!expired && !alreadyPosted) {
                try {
                    // Never overwrite a draft this tab already owns.
                    if (!window.localStorage.getItem(localDraftKey)) {
                        window.localStorage.setItem(localDraftKey, legacyRaw);
                    }
                } catch (_) { }
            }

            removeLocalDraftKey(localDraftBaseKey);
        }

        // Draft keys of tabs that were closed before their TTL elapsed would otherwise
        // stay in localStorage forever. Only expired/corrupt ones are removed.
        function cleanupStaleLocalDrafts() {
            if (!localDraftBaseKey) return;

            var prefix = localDraftBaseKey + '.';
            var ttlMs = localDraftTtlMinutes * 60 * 1000;
            var staleKeys = [];

            try {
                for (var i = 0; i < window.localStorage.length; i++) {
                    var key = window.localStorage.key(i);
                    if (!key || key.indexOf(prefix) !== 0) continue;

                    var data = readLocalDraftData(key);
                    var expired = !data || (ttlMs > 0 && (Date.now() - data.savedAt) > ttlMs);
                    if (expired) {
                        staleKeys.push(key);
                    }
                }
            } catch (_) {
                return;
            }

            staleKeys.forEach(function (key) {
                window.PosLocalDraft.removeEntry(localDraftBaseKey, key);
            });
        }

        function applyLocalDraftData(data) {
            if (!data) return;

            // Applying a draft dispatches input/change events so the rows recalculate,
            // and those events must not mark the draft as changed again (otherwise the
            // 12h TTL would be refreshed on every visit and the draft never expires).
            restoringLocalDraft = true;
            try {
                if (invoiceDateInput && data.invoiceDate) {
                    invoiceDateInput.value = data.invoiceDate;
                }
                if (paymentMethodSelect && data.paymentMethod) {
                    paymentMethodSelect.value = data.paymentMethod;
                    paymentMethodSelect.dispatchEvent(new Event('change', { bubbles: true }));
                }
                if (supplierSelect && data.supplierId) {
                    setSelectValue(supplierSelect, data.supplierId);
                }
                if (customerSelect && data.customerId) {
                    setSelectValue(customerSelect, data.customerId);
                }
                if (bankSelect && data.bankId) {
                    setSelectValue(bankSelect, data.bankId);
                }
                if (dueDateInput) {
                    dueDateInput.value = data.dueDate || '';
                }
                if (discountTypeInput && data.discountType) {
                    discountTypeInput.value = data.discountType;
                }
                if (discountValueInput) {
                    discountValueInput.value = data.discountValue || '';
                }
                if (noteInput) {
                    noteInput.value = data.note || '';
                }

                var lines = Array.isArray(data.lines) ? data.lines : [];
                if (lines.length) {
                    while (tableBody.querySelectorAll('tr').length < lines.length) {
                        addLine();
                    }
                    while (tableBody.querySelectorAll('tr').length > Math.max(lines.length, 1)) {
                        var rows = tableBody.querySelectorAll('tr');
                        if (rows.length > 1) {
                            rows[rows.length - 1].remove();
                        } else {
                            break;
                        }
                    }
                    reindexLines();

                    var rows = tableBody.querySelectorAll('tr');
                    rows.forEach(function (row, index) {
                        var line = lines[index] || {};
                        var itemSelect = row.querySelector('.item-select');
                        var qtyInput = row.querySelector('.qty-input');
                        var priceInput = row.querySelector('.price-input');
                        var sellPriceInput = row.querySelector('.sell-price-input');

                        if (itemSelect && line.itemId) {
                            setSelectValue(itemSelect, line.itemId);
                        }
                        if (qtyInput) {
                            qtyInput.value = line.qty || '';
                            qtyInput.dispatchEvent(new Event('input', { bubbles: true }));
                        }
                        if (priceInput) {
                            priceInput.value = line.unitPriceEur || '';
                            priceInput.dispatchEvent(new Event('input', { bubbles: true }));
                        }
                        if (sellPriceInput) {
                            sellPriceInput.value = line.sellPriceLyd || '';
                            sellPriceInput.dispatchEvent(new Event('input', { bubbles: true }));
                        }
                    });
                }
            } finally {
                restoringLocalDraft = false;
            }
        }

        function restoreLocalDraftIfAny() {
            if (!localDraftEnabled) return;

            window.PosLocalDraft.startHeartbeat();
            migrateLegacyLocalDraft();
            cleanupStaleLocalDrafts();

            var data = readLocalDraftData(localDraftKey);
            if (data) {
                var ttlMs = localDraftTtlMinutes * 60 * 1000;
                if (ttlMs > 0 && (Date.now() - data.savedAt) > ttlMs) {
                    window.PosLocalDraft.removeEntry(localDraftBaseKey, localDraftKey);
                } else if (!isFormEmptyForRestore()) {
                    // The form already has content (for example after a failed post):
                    // surface the draft so the user can delete it explicitly.
                    showLocalDraftBanner(data.savedAt, 'exists');
                    return;
                } else {
                    applyLocalDraftData(data);
                    showLocalDraftBanner(data.savedAt, 'restored');
                    calculateTotals();
                    return;
                }
            }

            if (!isFormEmptyForRestore()) return;

            // No draft for this tab: offer the most recent draft left behind by a
            // tab that was closed (or crashed) instead of restoring it silently.
            var orphan = window.PosLocalDraft.findOrphan(localDraftBaseKey, localDraftKey, localDraftTtlMinutes);
            if (orphan) {
                pendingOrphanDraftKey = orphan.key;
                showLocalDraftBanner(orphan.data.savedAt, 'orphan');
            }
        }

        function markDraftDirty() {
            if (restoringLocalDraft) return;
            if (!holdUrl && !localDraftEnabled) return;
            autoSaveDirty = true;
            scheduleAutoSave();
        }

        function scheduleAutoSave() {
            if (autoSaveTimer) {
                window.clearTimeout(autoSaveTimer);
            }
            autoSaveTimer = window.setTimeout(runAutoSave, autoSaveDelayMs);
        }

        async function runAutoSave() {
            if (!autoSaveDirty || autoSaveInFlight) return;
            if (holdUrl && !hasDraftableContent()) {
                autoSaveDirty = false;
                return;
            }

            if (!holdUrl && localDraftEnabled && !hasLocalDraftContent()) {
                autoSaveDirty = false;
                return;
            }

            autoSaveInFlight = true;
            autoSaveDirty = false;

            try {
                if (holdUrl) {
                    var result = await saveHoldState({ redirectOnSuccess: false, silent: true });
                    if (!result) {
                        autoSaveDirty = true;
                    }
                } else if (localDraftEnabled) {
                    var saved = saveLocalDraft();
                    if (!saved) {
                        autoSaveDirty = true;
                    }
                }
            } finally {
                autoSaveInFlight = false;
            }
        }

        async function saveHoldState(options) {
            var settings = options || {};
            var redirectOnSuccess = settings.redirectOnSuccess !== false;
            var silent = !!settings.silent;

            if (!holdUrl) {
                if (!silent) showWarning(msgHoldServiceUnavailable);
                return null;
            }

            try {
                var response = await fetch(holdUrl, {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': getToken(form)
                    },
                    body: new FormData(form)
                });

                var data = await response.json();
                if (!response.ok || !data.success) {
                    if (!silent) showWarning(data.error || msgUnableHoldInvoice);
                    return null;
                }

                if (data.draftId && draftIdInput) {
                    draftIdInput.value = data.draftId;
                }

                if (redirectOnSuccess) {
                    window.location.href = data.redirectUrl || newUrl || window.location.pathname;
                }

                return data;
            } catch (_) {
                if (!silent) showWarning(msgUnableHoldInvoice);
                return null;
            }
        }

        function addLine() {
            var index = tableBody.querySelectorAll('tr').length;
            var row = document.createElement('tr');

            if (lineTemplate.content) {
                var fragment = lineTemplate.content.cloneNode(true);
                fragment.querySelectorAll('[name]').forEach(function (field) {
                    field.name = field.name.replace(/__index__/g, index);
                });
                fragment.querySelectorAll('span[data-valmsg-for]').forEach(function (span) {
                    span.setAttribute('data-valmsg-for', span.getAttribute('data-valmsg-for').replace(/__index__/g, index));
                });
                row.appendChild(fragment);
            } else {
                row.innerHTML = lineTemplate.innerHTML.replace(/__index__/g, index);
            }

            tableBody.appendChild(row);

            applyDefaultPriceForRow(row, true);
            applyDefaultSellPriceForRow(row, true);

            if (window.jQuery && window.jQuery.validator && window.jQuery.validator.unobtrusive) {
                window.jQuery.validator.unobtrusive.parse(form);
            }

            document.dispatchEvent(new Event('ts:refresh'));
            calculateTotals();
            focusBarcodeInput();
            return row;
        }

        function removeLine(button) {
            var rows = tableBody.querySelectorAll('tr');
            var row = button.closest('tr');
            if (!row) return;

            if (rows.length <= 1) {
                var itemSelect = row.querySelector('.item-select');
                var qtyInput = row.querySelector('.qty-input');
                var priceInput = row.querySelector('.price-input');
                var sellPriceInput = row.querySelector('.sell-price-input');
                var lineTotalInput = row.querySelector('.line-total');

                setSelectValue(itemSelect, '');
                if (qtyInput) qtyInput.value = '';
                if (priceInput) priceInput.value = '';
                if (sellPriceInput) sellPriceInput.value = '';
                if (lineTotalInput) lineTotalInput.value = '0.00';

                calculateTotals();
                return;
            }

            row.remove();
            reindexLines();
            calculateTotals();
        }

        function getOrCreateRowByItem(itemId) {
            var rows = tableBody.querySelectorAll('tr');
            var emptyRow = null;

            for (var i = 0; i < rows.length; i++) {
                var itemSelect = rows[i].querySelector('.item-select');
                if (!itemSelect) continue;

                if (itemSelect.value === itemId) {
                    return rows[i];
                }

                if (!itemSelect.value && !emptyRow) {
                    emptyRow = rows[i];
                }
            }

            return emptyRow || addLine();
        }

        function addItemById(itemId) {
            var row = getOrCreateRowByItem(itemId);
            if (!row) return;

            var itemSelect = row.querySelector('.item-select');
            var prevQty = 0;
            var qtyInput = row.querySelector('.qty-input');
            if (qtyInput) {
                var parsedPrevQty = parseInt(qtyInput.value || '0', 10);
                prevQty = Number.isFinite(parsedPrevQty) && parsedPrevQty > 0 ? parsedPrevQty : 0;
            }

            setSelectValue(itemSelect, itemId);
            applyDefaultPriceForRow(row, true);

            var itemName = '';
            if (itemSelect) {
                var selectedOption = itemSelect.options[itemSelect.selectedIndex];
                itemName = selectedOption ? selectedOption.text : '';
            }

            var currentQty = parseInt(qtyInput ? qtyInput.value : '0', 10);
            var qtyAdded = 1;
            if (qtyInput) {
                var nextQty = Number.isFinite(currentQty) && currentQty > 0 ? currentQty + 1 : 1;
                qtyInput.value = String(nextQty);
                qtyInput.dispatchEvent(new Event('input', { bubbles: true }));
            }

            recordScanHistory({
                row: row,
                itemId: itemId,
                itemName: itemName,
                prevQty: prevQty
            });

            calculateTotals();
            playTone('success');
            showLastAdded(itemName, qtyAdded);
            setTimeout(function () { clearAndRefocusBarcodeInput(); }, 50);
        }

        async function resolveBarcode(code) {
            if (!resolveUrl) {
                showWarning(msgBarcodeServiceUnavailable);
                return;
            }

            hideWarning();
            hideUnknown();

            var response = await fetch(resolveUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getToken(form)
                },
                body: JSON.stringify({ code: code, mode: 'invoice' })
            });

            if (!response.ok) {
                showWarning(msgUnableResolveBarcode);
                return;
            }

            var data = await response.json();
            if (data.requiresMapping) {
                showUnknown(data);
                return;
            }

            if (!data.success || !data.itemId) {
                showWarning(data.error || msgBarcodeNotFound);
                return;
            }

            addItemById(data.itemId);
        }

        function enqueueBarcode(code) {
            var normalized = (code || '').trim();
            if (!normalized) return;
            barcodeQueue.push(normalized);
            clearAndRefocusBarcodeInput();
            processBarcodeQueue();
        }

        async function processBarcodeQueue() {
            if (barcodeResolving || !barcodeQueue.length) return;
            barcodeResolving = true;
            var nextCode = barcodeQueue.shift();
            try {
                await resolveBarcode(nextCode);
            } finally {
                barcodeResolving = false;
                if (barcodeQueue.length) {
                    processBarcodeQueue();
                }
            }
        }

        async function createCustomerInline() {
            if (!createCustomerUrl || !customerSelect || !quickCustomerNameInput) return;

            var name = (quickCustomerNameInput.value || '').trim();
            var phone = quickCustomerPhoneInput ? (quickCustomerPhoneInput.value || '').trim() : '';

            hideQuickCustomerError();

            if (!name) {
                showQuickCustomerError(msgCustomerNameRequired);
                return;
            }

            try {
                var response = await fetch(createCustomerUrl, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': getToken(form)
                    },
                    body: JSON.stringify({
                        name: name,
                        phone: phone
                    })
                });

                var data = await response.json();
                if (!response.ok || !data.success) {
                    showQuickCustomerError((data && data.error) || msgUnableCreateCustomer);
                    return;
                }

                var option = document.createElement('option');
                option.value = data.customerId;
                option.text = data.customerName;
                customerSelect.appendChild(option);
                customerSelect.value = data.customerId;
                customerSelect.dispatchEvent(new Event('change', { bubbles: true }));

                if (quickCustomerNameInput) quickCustomerNameInput.value = '';
                if (quickCustomerPhoneInput) quickCustomerPhoneInput.value = '';
                hideQuickCustomerError();

                if (quickCustomerModal) quickCustomerModal.hide();
            } catch (_) {
                showQuickCustomerError(msgUnableCreateCustomer);
            }
        }

        function stopCamera() {
            if (reader) reader.reset();
            scanning = false;
        }

        function startCamera() {
            if (!cameraModal || !barcodeVideo) return;
            if (!window.ZXing || !window.ZXing.BrowserMultiFormatReader) {
                showWarning(msgCameraUnavailable);
                return;
            }

            if (!reader) {
                reader = new window.ZXing.BrowserMultiFormatReader();
            }

            stopCamera();
            hideWarning();
            hideUnknown();

            lastScanText = '';
            lastScanAt = 0;
            scanning = true;
            cameraModal.show();

            reader.decodeFromVideoDevice(null, barcodeVideo, function (result) {
                if (!scanning || !result || !result.text) return;

                var now = Date.now();
                if (result.text === lastScanText && now - lastScanAt < 1200) return;

                lastScanText = result.text;
                lastScanAt = now;

                if (barcodeInput) {
                    barcodeInput.value = result.text.trim();
                }

                stopCamera();
                cameraModal.hide();
                enqueueBarcode(result.text.trim());
            });
        }

        tableBody.addEventListener('input', function (event) {
            if (event.target.matches('.qty-input, .price-input')) {
                calculateTotals();
            }
        });

        tableBody.addEventListener('change', function (event) {
            if (event.target.matches('.item-select')) {
                applyDefaultPriceForRow(event.target.closest('tr'), true);
                applyDefaultSellPriceForRow(event.target.closest('tr'), true);
                calculateTotals();
            }
        });

        if (rateInput) {
            rateInput.addEventListener('input', calculateTotals);
        }

        if (discountTypeInput) {
            discountTypeInput.addEventListener('change', calculateTotals);
        }

        if (discountValueInput) {
            discountValueInput.addEventListener('input', calculateTotals);
        }

        if (isReturnToggle) {
            isReturnToggle.addEventListener('change', applyReturnUiState);
        }

        tableBody.addEventListener('click', function (event) {
            var removeBtn = event.target.closest('.remove-line');
            if (removeBtn) {
                removeLine(removeBtn);
            }
        });

        if (addButton) {
            addButton.addEventListener('click', function () {
                addLine();
            });
        }

        if (barcodeAddBtn && barcodeInput) {
            var barcodeAutoTimer = null;
            var barcodeBurstCount = 0;
            var barcodeBurstStartAt = 0;
            var barcodeLastKeyAt = 0;
            var barcodeLastInputWasPaste = false;
            var barcodeAutoDelayMs = 70;
            var barcodeMinLength = 3;
            var barcodeMinBurstCount = 6;
            var barcodeMinBurstCountShort = 3;
            var barcodeMinLengthForShortBurst = 8;
            var barcodeMaxBurstDurationMs = 900;

            function clearBarcodeAutoTimer() {
                if (!barcodeAutoTimer) return;
                window.clearTimeout(barcodeAutoTimer);
                barcodeAutoTimer = null;
            }

            function resetBarcodeBurst() {
                barcodeBurstCount = 0;
                barcodeBurstStartAt = 0;
                barcodeLastKeyAt = 0;
                barcodeLastInputWasPaste = false;
            }

            function noteBarcodeKeyPress(event) {
                if (!event || !event.key || event.key.length !== 1) return;
                var now = Date.now();
                if (!barcodeLastKeyAt || now - barcodeLastKeyAt > 120) {
                    barcodeBurstCount = 1;
                    barcodeBurstStartAt = now;
                } else {
                    barcodeBurstCount += 1;
                }
                barcodeLastKeyAt = now;
            }

            function shouldAutoResolveBarcode(code) {
                if (!code || code.length < barcodeMinLength) return false;
                if (barcodeLastInputWasPaste) return true;
                if (!barcodeBurstStartAt || !barcodeLastKeyAt) return false;

                var burstDuration = barcodeLastKeyAt - barcodeBurstStartAt;
                if (burstDuration > barcodeMaxBurstDurationMs) return false;

                if (barcodeBurstCount >= barcodeMinBurstCount) return true;
                if (barcodeBurstCount >= barcodeMinBurstCountShort && code.length >= barcodeMinLengthForShortBurst) return true;
                return false;
            }

            function scheduleAutoResolveBarcode() {
                clearBarcodeAutoTimer();
                barcodeAutoTimer = window.setTimeout(function () {
                    var code = (barcodeInput.value || '').trim();
                    if (!shouldAutoResolveBarcode(code)) return;
                    resetBarcodeBurst();
                    enqueueBarcode(code);
                }, barcodeAutoDelayMs);
            }

            barcodeAddBtn.addEventListener('click', function () {
                clearBarcodeAutoTimer();
                resetBarcodeBurst();
                var code = (barcodeInput.value || '').trim();
                if (!code) {
                    showWarning(msgScanOrTypeFirst);
                    focusBarcodeInput();
                    return;
                }
                enqueueBarcode(code);
            });

            barcodeInput.addEventListener('keydown', function (event) {
                if (event.key === 'Enter' || event.key === 'Tab') {
                    event.preventDefault();
                    clearBarcodeAutoTimer();
                    resetBarcodeBurst();
                    var code = (barcodeInput.value || '').trim();
                    if (code) {
                        enqueueBarcode(code);
                    } else {
                        showWarning(msgScanOrTypeFirst);
                        focusBarcodeInput();
                    }
                    return;
                }

                noteBarcodeKeyPress(event);
            });

            barcodeInput.addEventListener('input', function (event) {
                barcodeLastInputWasPaste = event && (event.inputType === 'insertFromPaste' || (event.data && event.data.length > 1));
                scheduleAutoResolveBarcode();
            });
        }

        if (barcodeCameraBtn) {
            barcodeCameraBtn.addEventListener('click', startCamera);
        }

        if (barcodeUndoBtn) {
            barcodeUndoBtn.addEventListener('click', undoLastScan);
        }

        if (barcodeMapExistingBtn) {
            barcodeMapExistingBtn.addEventListener('click', function () {
                mapUnknownBarcodeToExistingItem();
            });
        }

        if (cameraModalEl) {
            cameraModalEl.addEventListener('hidden.bs.modal', stopCamera);
        }

        if (quickCustomerSaveBtn) {
            quickCustomerSaveBtn.addEventListener('click', createCustomerInline);
        }

        if (quickCustomerModalEl) {
            quickCustomerModalEl.addEventListener('show.bs.modal', hideQuickCustomerError);
        }

        if (holdInvoiceBtn) {
            holdInvoiceBtn.addEventListener('click', function () {
                saveHoldState({ redirectOnSuccess: true, silent: false });
            });
        }

        if (restoreInvoiceBtn) {
            restoreInvoiceBtn.addEventListener('click', resetToNewSale);
        }

        if (localDraftDiscardBtn) {
            localDraftDiscardBtn.addEventListener('click', discardLocalDraft);
        }

        if (localDraftRecoverBtn) {
            localDraftRecoverBtn.addEventListener('click', recoverLocalDraft);
        }

        if (paymentMethodInputs && paymentMethodInputs.length) {
            paymentMethodInputs.forEach(function (input) {
                input.addEventListener('change', syncCustomerRequirement);
            });
        }

        form.addEventListener('input', markDraftDirty);
        form.addEventListener('change', markDraftDirty);
        document.addEventListener('visibilitychange', function () {
            if (document.hidden) {
                runAutoSave();
            }
        });

        tableBody.querySelectorAll('tr').forEach(function (row) {
            applyDefaultPriceForRow(row, false);
            applyDefaultSellPriceForRow(row, false);
        });

        restoreLocalDraftIfAny();
        syncCustomerRequirement();
        applyReturnUiState();
        calculateTotals();
        initializeSoundToggle();
        installBarcodeFirstMode();
        focusBarcodeInput();
    }

    document.querySelectorAll('form').forEach(initInvoiceForm);
})();
