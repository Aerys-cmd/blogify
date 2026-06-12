// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener('submit', function (event) {
    var submitter = event.submitter;
    var form = event.target;
    var message = submitter?.dataset.confirmMessage || form?.dataset.confirmMessage;
    if (!message || form.dataset.confirmed === 'true') return;

    event.preventDefault();
    var dialog = document.getElementById('b-confirm-dialog');
    if (!dialog) {
        dialog = document.createElement('dialog');
        dialog.id = 'b-confirm-dialog';
        dialog.className = 'b-dialog';
        var title = document.body.dataset.confirmTitle || 'Confirm action';
        var cancel = document.body.dataset.confirmCancel || 'Cancel';
        var confirmAction = document.body.dataset.confirmAction || 'Confirm';
        dialog.innerHTML = '<form method="dialog"><h2></h2><p data-message></p><div><button value="cancel" class="btn btn-outline-secondary"></button><button value="confirm" class="btn btn-danger"></button></div></form>';
        dialog.querySelector('h2').textContent = title;
        dialog.querySelector('[value="cancel"]').textContent = cancel;
        dialog.querySelector('[value="confirm"]').textContent = confirmAction;
        document.body.appendChild(dialog);
    }
    dialog.querySelector('[data-message]').textContent = message;
    dialog.addEventListener('close', function onClose() {
        dialog.removeEventListener('close', onClose);
        if (dialog.returnValue === 'confirm') {
            form.dataset.confirmed = 'true';
            if (submitter) form.requestSubmit(submitter); else form.submit();
        }
    });
    dialog.showModal();
});

document.addEventListener('click', function (event) {
    var button = event.target.closest('[data-toggle-password]');
    if (!button) return;
    var input = document.getElementById(button.dataset.togglePassword);
    if (!input) return;
    var reveal = input.type === 'password';
    input.type = reveal ? 'text' : 'password';
    button.textContent = reveal ? 'Hide' : 'Show';
    button.setAttribute('aria-pressed', reveal ? 'true' : 'false');
});
