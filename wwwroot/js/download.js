window.downloadFileFromBytes = (fileName, base64Data, contentType) => {
    const mime = contentType || 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
    const byteCharacters = atob(base64Data);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: mime });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
};

window.copyTextToClipboard = async (text) => {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
};

window.heteroStorage = {
    get: (key) => { try { return localStorage.getItem(key); } catch { return null; } },
    set: (key, val) => { try { localStorage.setItem(key, val); } catch { } },
    remove: (key) => { try { localStorage.removeItem(key); } catch { } }
};
