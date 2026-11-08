/**
 * CinemaHub Frontend Security Utilities
 * Chống XSS, cung cấp hàm Escape HTML và quản lý CSRF Token an toàn
 */
(function (window) {
    'use strict';

    var entityMap = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#39;',
        '/': '&#x2F;'
    };

    window.CinemaSecurity = {
        escapeHtml: function (str) {
            if (str === null || str === undefined) return '';
            return String(str).replace(/[&<>"'\/]/g, function (s) {
                return entityMap[s];
            });
        },

        sanitizeUrl: function (url) {
            if (!url) return '';
            var trimmed = String(url).trim();
            // Chỉ chấp nhận scheme http:, https:, đường dẫn tương đối '/', hoặc action query:
            if (/^(https?:\/\/|\/|query:)/i.test(trimmed)) {
                return trimmed;
            }
            return '#';
        },

        getCsrfToken: function () {
            var meta = document.querySelector('meta[name="csrf-token"]');
            if (meta) {
                var content = meta.getAttribute('content');
                if (content) return content;
            }
            var input = document.querySelector('input[name="__RequestVerificationToken"]');
            return input ? input.value : '';
        }
    };

    // Tự động gắn RequestVerificationToken cho toàn bộ request AJAX của jQuery
    if (window.jQuery) {
        window.jQuery(document).ajaxSend(function (event, jqxhr, settings) {
            if (!/^(GET|HEAD|OPTIONS|TRACE)$/i.test(settings.type) && !settings.crossDomain) {
                var token = window.CinemaSecurity.getCsrfToken();
                if (token) {
                    jqxhr.setRequestHeader("RequestVerificationToken", token);
                }
            }
        });
    }
})(window);
