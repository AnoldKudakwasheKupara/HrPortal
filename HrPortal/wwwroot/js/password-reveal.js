// Lets someone check what they typed into a password field. The
// button only appears where the markup asks for one, and the field
// goes back to hidden on submit so a password is never left on
// screen after the form is sent.
(function () {
    'use strict';

    function iconFor(revealed) {
        return revealed ? 'fa-eye' : 'fa-eye-slash';
    }

    function apply(button, field, revealed) {
        field.type = revealed ? 'text' : 'password';
        button.setAttribute('aria-pressed', revealed ? 'true' : 'false');
        button.setAttribute(
            'aria-label',
            revealed ? 'Hide password' : 'Show password');

        var icon = button.querySelector('i');

        if (icon) {
            icon.classList.remove('fa-eye', 'fa-eye-slash');
            icon.classList.add(iconFor(revealed));
        }
    }

    document.addEventListener('click', function (event) {
        var button = event.target.closest('[data-reveal]');

        if (!button) {
            return;
        }

        var field = document.getElementById(button.dataset.reveal);

        if (!field) {
            return;
        }

        apply(button, field, field.type === 'password');
    });

    // Listening on the way down. Validation runs on the form itself
    // and returns false when the form is not valid, which stops the
    // event before it would reach a listener bubbling up here.
    document.addEventListener('submit', function (event) {
        var form = event.target;

        if (!form || typeof form.querySelectorAll !== 'function') {
            return;
        }

        form.querySelectorAll('[data-reveal]').forEach(function (button) {
            var field = document.getElementById(button.dataset.reveal);

            if (field && field.type === 'text') {
                apply(button, field, false);
            }
        });
    }, true);
}());
