// تهيئة جميع  Carousels
$(".latest-news-carousel, .owl-carousel-3").owlCarousel({
    loop: true,
    margin: 10,
    autoplay: true,
    autoplayTimeout: 2500,
    dots: false,
    responsive: {
        0: { items: 1 },
        576: { items: 2 },
        992: { items: 3 }
    }
});

// إضافة تأثير Fade/Slide عند التمرير
function handleFadeSlide() {
    const elements = document.querySelectorAll('.fade-slide');
    const windowHeight = window.innerHeight;

    elements.forEach(el => {
        const positionFromTop = el.getBoundingClientRect().top;
        if (positionFromTop - windowHeight <= -100) {
            el.classList.add('show');
        }
    });
}

window.addEventListener('scroll', handleFadeSlide);
window.addEventListener('load', handleFadeSlide);