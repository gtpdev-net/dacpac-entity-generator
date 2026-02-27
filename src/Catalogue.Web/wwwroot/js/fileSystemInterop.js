// fileSystemInterop.js — File System Access API helpers for EF Generation page
// Requires Chromium (Chrome/Edge) — acceptable for a local dev tool.

// Stored handles survive JS-to-Blazor round trips within the same browser session
let _entityDirHandle = null;
let _dbContextFileHandle = null;

export function isFileSystemAccessSupported() {
    return typeof window.showDirectoryPicker === 'function';
}

export async function selectEntityDirectory() {
    try {
        _entityDirHandle = await window.showDirectoryPicker({ mode: 'readwrite' });
        return _entityDirHandle.name;
    } catch (e) {
        if (e.name === 'AbortError') return null;
        console.error('selectEntityDirectory error:', e);
        return null;
    }
}

export async function selectDbContextFile() {
    try {
        [_dbContextFileHandle] = await window.showOpenFilePicker({
            types: [{ description: 'C# Files', accept: { 'text/plain': ['.cs'] } }],
            multiple: false
        });
        return _dbContextFileHandle.name;
    } catch (e) {
        if (e.name === 'AbortError') return null;
        console.error('selectDbContextFile error:', e);
        return null;
    }
}

export async function writeFilesToDirectory(files) {
    if (!_entityDirHandle) throw new Error('No entity directory selected.');

    for (const file of files) {
        const fileHandle = await _entityDirHandle.getFileHandle(file.name, { create: true });
        const writable = await fileHandle.createWritable();
        await writable.write(file.content);
        await writable.close();
    }
}

export async function writeDbContextFile(content) {
    if (!_dbContextFileHandle) throw new Error('No DbContext file selected.');
    const writable = await _dbContextFileHandle.createWritable();
    await writable.write(content);
    await writable.close();
}
