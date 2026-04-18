(function ($) {
    if (!$.validator) {
        return;
    }

    function parseDate(value) {
        var parts = value.split("/");
        if (parts.length !== 3) {
            return null;
        }

        var day = parseInt(parts[0], 10);
        var month = parseInt(parts[1], 10) - 1;
        var year = parseInt(parts[2], 10);

        if (!day || month < 0 || month > 11 || !year) {
            return null;
        }

        var date = new Date(Date.UTC(year, month, day, 12, 0, 0, 0));
        if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month || date.getUTCDate() !== day) {
            return null;
        }

        return date;
    }

    $.validator.methods.date = function (value, element) {
        if (this.optional(element)) {
            return true;
        }

        return !!parseDate(value);
    };
})(jQuery);
