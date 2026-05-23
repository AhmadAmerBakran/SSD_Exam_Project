const state = {
    csrfToken: sessionStorage.getItem('csrfToken') || '',
    user: null
};

const maxFileSize = 5 * 1024 * 1024;
const allowedExtensions = ['.pdf', '.png', '.jpg', '.jpeg', '.txt'];
const allowedMimeTypes = ['application/pdf', 'image/png', 'image/jpeg', 'image/jpg', 'text/plain', 'application/octet-stream'];

const $ = (id) => document.getElementById(id);

function showMessage(text, isError = false) {
    const el = $('message');
    el.textContent = text;
    el.classList.remove('hidden', 'error');
    if (isError) el.classList.add('error');
}

function hideMessage() {
    $('message').classList.add('hidden');
}

function setAuth(user, csrfToken) {
    state.user = user;
    state.csrfToken = csrfToken;
    sessionStorage.setItem('csrfToken', csrfToken);
    $('authView').classList.add('hidden');
    $('appView').classList.remove('hidden');
    $('sessionPanel').classList.remove('hidden');
    $('currentUser').textContent = `${user.fullName} (${user.email})`;
    loadFiles();
}

function clearAuth() {
    state.user = null;
    state.csrfToken = '';
    sessionStorage.removeItem('csrfToken');
    $('authView').classList.remove('hidden');
    $('appView').classList.add('hidden');
    $('sessionPanel').classList.add('hidden');
    $('filesList').replaceChildren();
}

async function apiFetch(url, options = {}) {
    const method = (options.method || 'GET').toUpperCase();
    const headers = new Headers(options.headers || {});

    if (method !== 'GET' && state.csrfToken) {
        headers.set('X-CSRF-Token', state.csrfToken);
    }

    const response = await fetch(url, {
        ...options,
        method,
        headers,
        credentials: 'same-origin'
    });

    if (response.status === 401) {
        clearAuth();
        throw new Error('You are not logged in or your session expired.');
    }

    let body = null;
    const contentType = response.headers.get('content-type') || '';
    if (contentType.includes('application/json')) {
        body = await response.json();
    }

    if (!response.ok) {
        const errorText = body?.errors?.length ? body.errors.join(' ') : (body?.message || body?.title || `Request failed with status ${response.status}`);
        throw new Error(errorText);
    }

    return body;
}

function validateEmail(email) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) && email.length <= 254;
}

function getPasswordRules(password, confirmPassword, email) {
    const local = (email || '').split('@')[0] || '';
    return [
        { text: 'At least 12 characters', ok: password.length >= 12 },
        { text: 'At most 128 characters', ok: password.length <= 128 },
        { text: 'Contains uppercase letter', ok: /[A-Z]/.test(password) },
        { text: 'Contains lowercase letter', ok: /[a-z]/.test(password) },
        { text: 'Contains number', ok: /\d/.test(password) },
        { text: 'Contains special character', ok: /[^A-Za-z0-9]/.test(password) },
        { text: 'Does not contain email username', ok: !local || local.length < 4 || !password.toLowerCase().includes(local.toLowerCase()) },
        { text: 'Password confirmation matches', ok: password.length > 0 && password === confirmPassword }
    ];
}

function renderPasswordRules() {
    const password = $('registerPassword').value;
    const confirm = $('registerConfirmPassword').value;
    const email = $('registerEmail').value;
    const rules = getPasswordRules(password, confirm, email);
    const list = $('passwordRules');
    list.replaceChildren();
    for (const rule of rules) {
        const li = document.createElement('li');
        li.textContent = rule.text;
        li.className = rule.ok ? 'ok' : 'bad';
        list.appendChild(li);
    }
}

function validateFile(file) {
    if (!file) return 'Choose a file first.';
    if (file.size <= 0) return 'The selected file is empty.';
    if (file.size > maxFileSize) return 'The selected file is larger than 5 MB.';

    const lowerName = file.name.toLowerCase();
    const extension = allowedExtensions.find(ext => lowerName.endsWith(ext));
    if (!extension) return 'Only PDF, PNG, JPG/JPEG and TXT files are allowed.';
    if (file.name.length > 150) return 'File name must not exceed 150 characters.';
    if (/[<>"']/.test(file.name)) return 'File name contains unsafe characters.';
    if (file.type && !allowedMimeTypes.includes(file.type)) return 'File MIME type is not allowed.';
    return null;
}

$('registerPassword').addEventListener('input', renderPasswordRules);
$('registerConfirmPassword').addEventListener('input', renderPasswordRules);
$('registerEmail').addEventListener('input', renderPasswordRules);
renderPasswordRules();

$('registerForm').addEventListener('submit', async (event) => {
    event.preventDefault();
    hideMessage();

    const payload = {
        fullName: $('registerFullName').value.trim(),
        email: $('registerEmail').value.trim(),
        password: $('registerPassword').value,
        confirmPassword: $('registerConfirmPassword').value
    };

    if (payload.fullName.length < 2 || payload.fullName.length > 120) return showMessage('Full name must be between 2 and 120 characters.', true);
    if (!validateEmail(payload.email)) return showMessage('Enter a valid email address.', true);
    const rules = getPasswordRules(payload.password, payload.confirmPassword, payload.email);
    if (!rules.every(r => r.ok)) return showMessage('Password does not meet all requirements.', true);

    try {
        const result = await apiFetch('/api/auth/register', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        setAuth(result.user, result.csrfToken);
        showMessage('Account created and logged in.');
    } catch (error) {
        showMessage(error.message, true);
    }
});

$('loginForm').addEventListener('submit', async (event) => {
    event.preventDefault();
    hideMessage();

    const payload = {
        email: $('loginEmail').value.trim(),
        password: $('loginPassword').value
    };

    if (!validateEmail(payload.email)) return showMessage('Enter a valid email address.', true);
    if (!payload.password) return showMessage('Password is required.', true);

    try {
        const result = await apiFetch('/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        setAuth(result.user, result.csrfToken);
        showMessage('Logged in.');
    } catch (error) {
        showMessage(error.message, true);
    }
});

$('logoutBtn').addEventListener('click', async () => {
    try {
        await apiFetch('/api/auth/logout', { method: 'POST' });
    } catch (_) {
        // The local UI should be cleared even if the server session is already gone.
    }
    clearAuth();
    showMessage('Logged out.');
});

$('uploadForm').addEventListener('submit', async (event) => {
    event.preventDefault();
    hideMessage();
    const file = $('fileInput').files[0];
    const validationError = validateFile(file);
    if (validationError) return showMessage(validationError, true);

    const formData = new FormData();
    formData.append('file', file);

    try {
        await apiFetch('/api/files', { method: 'POST', body: formData });
        $('fileInput').value = '';
        showMessage('File uploaded securely.');
        await loadFiles();
    } catch (error) {
        showMessage(error.message, true);
    }
});

$('refreshBtn').addEventListener('click', loadFiles);

async function loadFiles() {
    try {
        const files = await apiFetch('/api/files');
        renderFiles(files);
    } catch (error) {
        showMessage(error.message, true);
    }
}

function renderFiles(files) {
    const list = $('filesList');
    list.replaceChildren();

    if (!files.length) {
        const empty = document.createElement('p');
        empty.className = 'muted';
        empty.textContent = 'No files uploaded yet.';
        list.appendChild(empty);
        return;
    }

    for (const file of files) {
        const item = document.createElement('article');
        item.className = 'file-item';

        const head = document.createElement('div');
        head.className = 'file-head';

        const info = document.createElement('div');
        const name = document.createElement('div');
        name.className = 'file-name';
        name.textContent = file.originalFileName;

        const meta = document.createElement('div');
        meta.className = 'file-meta';
        meta.textContent = `${file.contentType} · ${formatBytes(file.sizeBytes)} · SHA-256: ${file.sha256Hash}`;

        info.append(name, meta);
        head.appendChild(info);

        const actions = document.createElement('div');
        actions.className = 'actions';

        const download = document.createElement('a');
        download.href = `/api/files/${file.id}/download`;
        download.textContent = 'Download';
        download.className = 'button-link';

        const share = document.createElement('button');
        share.type = 'button';
        share.className = 'small';
        share.textContent = 'Create temporary link';
        share.addEventListener('click', () => createShareLink(file.id, item));

        const remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'small danger';
        remove.textContent = 'Delete';
        remove.addEventListener('click', () => deleteFile(file.id));

        actions.append(download, share, remove);
        item.append(head, actions);
        list.appendChild(item);
    }
}

async function createShareLink(fileId, item) {
    hideMessage();
    try {
        const share = await apiFetch(`/api/files/${fileId}/shares`, { method: 'POST' });
        let result = item.querySelector('.share-result');
        if (!result) {
            result = document.createElement('div');
            result.className = 'share-result';
            item.appendChild(result);
        }
        result.replaceChildren();

        const text = document.createElement('p');
        text.textContent = `Temporary link expires: ${new Date(share.expiresAtUtc).toLocaleString()} · Max downloads: ${share.maxDownloads}`;
        const link = document.createElement('a');
        link.href = share.url;
        link.target = '_blank';
        link.rel = 'noopener noreferrer';
        link.textContent = share.url;
        result.append(text, link);

        try {
            await navigator.clipboard.writeText(share.url);
            showMessage('Share link created and copied to clipboard.');
        } catch (_) {
            showMessage('Share link created.');
        }
    } catch (error) {
        showMessage(error.message, true);
    }
}

async function deleteFile(fileId) {
    if (!confirm('Delete this file?')) return;
    hideMessage();
    try {
        await apiFetch(`/api/files/${fileId}`, { method: 'DELETE' });
        showMessage('File deleted.');
        await loadFiles();
    } catch (error) {
        showMessage(error.message, true);
    }
}

function formatBytes(bytes) {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

(async function bootstrap() {
    try {
        const result = await apiFetch('/api/auth/me');
        setAuth(result.user, result.csrfToken);
    } catch (_) {
        clearAuth();
    }
})();
