(function () {
    function initDatePickers(container) {
        if (typeof flatpickr === "undefined") {
            return;
        }

        var scope = container || document;
        var lang = (document.documentElement.getAttribute("lang") || "").toLowerCase();
        var isRtl = (document.documentElement.getAttribute("dir") || "").toLowerCase() === "rtl";
        var localeOption = "default";

        if (lang.startsWith("ar") && flatpickr.l10ns && flatpickr.l10ns.ar) {
            localeOption = flatpickr.l10ns.ar;
        }

        var inputs = scope.querySelectorAll(".js-date");
        inputs.forEach(function (input) {
            if (input._flatpickr) {
                return;
            }

            flatpickr(input, {
                dateFormat: "d/m/Y",
                allowInput: true,
                disableMobile: true,
                locale: localeOption,
                positionElement: isRtl ? input : undefined
            });
        });
    }

    window.initDatePickers = initDatePickers;

    document.addEventListener("DOMContentLoaded", function () {
        initDatePickers(document);
    });
})();
