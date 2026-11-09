// RaceDay: small, dependency-light helpers shared by every page.
(function () {
    'use strict';

    // 1. Confirmation dialog for destructive forms: <form data-confirm="Delete this event?">
    var modalEl = document.getElementById('confirmModal');
    if (modalEl && window.bootstrap) {
        var modal = new bootstrap.Modal(modalEl);
        var pendingForm = null;

        document.addEventListener('submit', function (e) {
            var form = e.target;
            if (!(form instanceof HTMLFormElement) || !form.dataset.confirm || form.dataset.confirmed === 'true') return;
            e.preventDefault();
            pendingForm = form;
            document.getElementById('confirmTitle').textContent = form.dataset.confirmTitle || 'Are you sure?';
            document.getElementById('confirmBody').textContent = form.dataset.confirm;
            document.getElementById('confirmYes').textContent = form.dataset.confirmButton || 'Yes, continue';
            modal.show();
        });

        document.getElementById('confirmYes').addEventListener('click', function () {
            if (!pendingForm) return;
            pendingForm.dataset.confirmed = 'true';
            modal.hide();
            pendingForm.submit();
        });
    }

    // 2. Success messages fade away by themselves; errors stay until dismissed.
    document.querySelectorAll('[data-autodismiss="true"]').forEach(function (el) {
        setTimeout(function () {
            if (window.bootstrap) { bootstrap.Alert.getOrCreateInstance(el).close(); }
        }, 6000);
    });

    // 3. Banner picker: check type and size in the browser, and show a preview.
    var picker = document.querySelector('[data-banner-input]');
    if (picker) {
        var maxBytes = 5 * 1024 * 1024;
        var allowed = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];
        var message = document.querySelector('[data-banner-message]');
        var preview = document.querySelector('[data-banner-preview]');

        picker.addEventListener('change', function () {
            var file = picker.files && picker.files[0];
            if (message) message.textContent = '';
            if (!file) { if (preview) preview.hidden = true; return; }

            if (allowed.indexOf(file.type) === -1) {
                if (message) message.textContent = 'Please choose a JPG, PNG, WEBP or GIF image.';
                picker.value = ''; if (preview) preview.hidden = true; return;
            }
            if (file.size > maxBytes) {
                if (message) message.textContent = 'That image is larger than 5 MB. Please choose a smaller one.';
                picker.value = ''; if (preview) preview.hidden = true; return;
            }
            if (preview) {
                preview.src = URL.createObjectURL(file);
                preview.hidden = false;
            }
        });
    }

    // 4. Stop double submits (two enrolments, two results) by disabling the button after the first click.
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement) || e.defaultPrevented) return;
        var button = form.querySelector('button[type="submit"][data-busy-text]');
        if (button) {
            setTimeout(function () {
                button.disabled = true;
                button.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>' + button.dataset.busyText;
            }, 0);
        }
    });
})();
