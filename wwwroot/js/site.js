// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// 1. Helper to trigger the hidden file input
function evaTriggerUpload(type, typeInputId, fileInputId) {
    const typeInput = document.getElementById(typeInputId);
    const fileInput = document.getElementById(fileInputId);

    if (!typeInput || !fileInput) {
        console.error("Upload inputs not found", typeInputId, fileInputId);
        return;
    }

    typeInput.value = type;
    // Allow selecting the same file repeatedly across different document types or retries.
    fileInput.value = '';
    fileInput.click();
}

function evaShowPageFeedback(message, kind = "success") {
    const feedback = document.getElementById("page-feedback");
    if (!feedback) {
        return;
    }

    feedback.innerHTML = `
        <div class="alert alert-${kind} mb-4 shadow-sm" role="alert">
            <i class="bi bi-${kind === "success" ? "check-circle-fill" : "exclamation-triangle-fill"} me-2"></i>${message}
        </div>`;

    feedback.scrollIntoView({ behavior: "smooth", block: "start" });
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
            form.reset();
            const isDelete = (form.getAttribute("action") || "").includes("DeleteDoc");
            const message = isDelete
                ? 'Documento removido da edição. Quando terminar, clique em "Enviar para análise".'
                : 'Documento salvo na edição. Quando terminar, clique em "Enviar para análise".';
            evaShowPageFeedback(message);
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
