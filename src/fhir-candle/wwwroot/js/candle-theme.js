// Copyright (c) Microsoft Corporation. Licensed under the MIT License.
// The plaintext legacy "theme" object is canonical; initialization is read-only.
(() => {
    const listeners = new Map();
    let nextId = 0;
    const media = window.matchMedia?.("(prefers-color-scheme: dark)");
    const read = () => {
        try { return window.localStorage.getItem("theme"); }
        catch { return null; }
    };
    const object = json => {
        try {
            const value = JSON.parse(json);
            return value && typeof value === "object" && !Array.isArray(value) ? value : null;
        } catch { return null; }
    };
    const apply = dark => {
        // Independent of the vendor initializer's document/body theme attributes.
        document.documentElement.dataset.candleTheme = dark ? "dark" : "light";
        document.documentElement.style.colorScheme = dark ? "dark" : "light";
    };
    const early = object(read()) ?? {};
    apply(early.mode === "dark" || (early.mode !== "light" && !!media?.matches));

    window.candleTheme = {
        connect(callback) {
            const id = ++nextId;
            const json = read();
            const changed = event => callback.invokeMethodAsync("SystemChanged", event.matches);
            media?.addEventListener("change", changed);
            listeners.set(id, { changed, revision: 0 });
            return { id, json, isSystemDark: !!media?.matches };
        },
        apply(id, dark) {
            if (listeners.has(id)) apply(dark);
        },
        persist(id, json, revision) {
            const listener = listeners.get(id);
            if (!listener || revision <= listener.revision) return;
            const incoming = object(json) ?? {};
            // Only mode belongs to this writer. Re-read before every synchronous write;
            // any valid object (including {}) owns the other fields, not the managed snapshot.
            const value = object(read()) ?? incoming;
            value.mode = incoming.mode;
            const updated = JSON.stringify(value);
            try { window.localStorage.setItem("theme", updated); }
            catch { /* Storage denial is a usable in-memory preference, not a fatal JS error. */ }
            listener.revision = revision;
        },
        disconnect(id) {
            const listener = listeners.get(id);
            if (!listener) return;
            media?.removeEventListener("change", listener.changed);
            listeners.delete(id);
        }
    };
})();
