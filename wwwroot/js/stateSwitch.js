

$(document).ready(function () {
    $("#State").on("change", function () {
        if ($(this).is(":checked")) {
            $(this).parent().parent().addClass("bg-success-subtle");
            $(this).parent().parent().addClass("text-success");
            $(this).parent().parent().removeClass("bg-danger-subtle");
            $(this).parent().parent().removeClass("text-danger");
        } else {
            $(this).parent().parent().addClass("bg-danger-subtle");
            $(this).parent().parent().addClass("text-danger");
            $(this).parent().parent().removeClass("bg-success-subtle");
            $(this).parent().parent().removeClass("text-success");
        }
    });
});
