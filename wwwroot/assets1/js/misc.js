(function($) {
  'use strict';
  $(function() {
    var body = $('body');
    var contentWrapper = $('.content-wrapper');
    var scroller = $('.container-scroller');
    var footer = $('.footer');
    var sidebar = $('.sidebar');

    //Add active class to the single best matching nav-link based on current path.
    function normalizePath(path) {
      if (!path) return '/';
      var normalized = String(path).toLowerCase().replace(/\/+$/, '');
      return normalized.length ? normalized : '/';
    }

    function markActive(element) {
      element.parents('.nav-item').last().addClass('active');
      if (element.parents('.sub-menu').length) {
        element.closest('.collapse').addClass('show');
        element.addClass('active');
      }
      if (element.parents('.submenu-item').length) {
        element.addClass('active');
      }
    }

    var currentPath = normalizePath(window.location.pathname);
    var navLinks = $('.nav li a', sidebar).add($('.horizontal-menu .nav li a'));
    var bestMatch = null;
    var bestScore = 0;
    var bestLength = -1;

    navLinks.each(function() {
      var element = $(this);
      var href = element.attr('href');
      if (!href || href === '#' || href.indexOf('javascript:') === 0) {
        return;
      }

      var linkUrl;
      try {
        linkUrl = new URL(href, window.location.origin);
      } catch (e) {
        return;
      }

      var linkPath = normalizePath(linkUrl.pathname);
      var score = 0;

      if (linkPath === currentPath) {
        score = 3;
      } else if (currentPath.indexOf(linkPath + '/') === 0) {
        score = 2;
      }

      if (score > 0 && (score > bestScore || (score === bestScore && linkPath.length > bestLength))) {
        bestMatch = element;
        bestScore = score;
        bestLength = linkPath.length;
      }
    });

    if (bestMatch) {
      markActive(bestMatch);
    }

    //Close other submenu in sidebar on opening any

    sidebar.on('show.bs.collapse', '.collapse', function() {
      sidebar.find('.collapse.show').collapse('hide');
    });

    $(".aside-toggler").on("click", function () {
      $(".mail-sidebar,.chat-list-wrapper").toggleClass("menu-open");
    });


    //Change sidebar and content-wrapper height
    applyStyles();

    function applyStyles() {
      //Applying perfect scrollbar
      if (!body.hasClass("rtl")) {
        if ($('.settings-panel .tab-content .tab-pane.scroll-wrapper').length) {
          const settingsPanelScroll = new PerfectScrollbar('.settings-panel .tab-content .tab-pane.scroll-wrapper');
        }
        if ($('.chats').length) {
          const chatsScroll = new PerfectScrollbar('.chats');
        }
        if (body.hasClass("sidebar-fixed")) {
          var fixedSidebarScroll = new PerfectScrollbar('#sidebar .nav');
        }
      }
    }

    $('[data-toggle="minimize"]').on("click", function() {
      if ((body.hasClass('sidebar-toggle-display')) || (body.hasClass('sidebar-absolute'))) {
        body.toggleClass('sidebar-hidden');
      } else {
        body.toggleClass('sidebar-icon-only');
      }
    });

    //checkbox and radios
    $(".form-check label,.form-radio label").append('<i class="input-helper"></i>');

    //fullscreen
    $("#fullscreen-button").on("click", function toggleFullScreen() {
      if ((document.fullScreenElement !== undefined && document.fullScreenElement === null) || (document.msFullscreenElement !== undefined && document.msFullscreenElement === null) || (document.mozFullScreen !== undefined && !document.mozFullScreen) || (document.webkitIsFullScreen !== undefined && !document.webkitIsFullScreen)) {
        if (document.documentElement.requestFullScreen) {
          document.documentElement.requestFullScreen();
        } else if (document.documentElement.mozRequestFullScreen) {
          document.documentElement.mozRequestFullScreen();
        } else if (document.documentElement.webkitRequestFullScreen) {
          document.documentElement.webkitRequestFullScreen(Element.ALLOW_KEYBOARD_INPUT);
        } else if (document.documentElement.msRequestFullscreen) {
          document.documentElement.msRequestFullscreen();
        }
      } else {
        if (document.cancelFullScreen) {
          document.cancelFullScreen();
        } else if (document.mozCancelFullScreen) {
          document.mozCancelFullScreen();
        } else if (document.webkitCancelFullScreen) {
          document.webkitCancelFullScreen();
        } else if (document.msExitFullscreen) {
          document.msExitFullscreen();
        }
      }
    })
  });
})(jQuery);