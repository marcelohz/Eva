// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// 1. Helper to trigger the hidden file input
function evaTriggerUpload(type, typeInputId, fileInputId) {
    document.getElementById(typeInputId).value = type;
    document.getElementById(fileInputId).click();
}

// 2. Generic Async POST (for Uploads and Deletions)
async function evaPerformAsyncAction(formId, containerId) {
    const form = document.getElementById(formId);
    const container = document.getElementById(containerId);

    if (!form || !container) {
        console.error("Form or container not found", formId, containerId);
        return;
    }

    const formData = new FormData(form);
    const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');

    if (!tokenInput) {
        console.error("Antiforgery token missing in form " + formId);
        return;
    }

    // Visual feedback while the server processes the file
    container.style.opacity = '0.5';
    container.style.pointerEvents = 'none';

    try {
        const response = await fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: { "RequestVerificationToken": tokenInput.value }
        });

        if (response.ok) {
            const html = await response.text();
            container.innerHTML = html; // Inject the new document list safely
        } else {
            console.error("Server returned status: " + response.status);
            alert("Erro ao processar o arquivo. A página será recarregada por segurança.");
            window.location.reload();
        }
    } catch (error) {
        console.error("Async action error:", error);
        alert("Erro de conexão ao enviar o arquivo.");
    } finally {
        container.style.opacity = '1';
        container.style.pointerEvents = 'auto';
    }
}