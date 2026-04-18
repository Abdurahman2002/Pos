function handleFadeSlide() {
    var windowHeight = $(window).height();
    $('.fade-slide').each(function () {
        var position = $(this).offset().top - $(window).scrollTop();
        if (position - windowHeight <= -100) {
            $(this).addClass('show');
        }
    });
}

$(window).on('scroll load', handleFadeSlide);