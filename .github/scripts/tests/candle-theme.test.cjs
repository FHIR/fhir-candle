// <copyright file="candle-theme.test.cjs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { resolve } = require("node:path");
const { test } = require("node:test");
const vm = require("node:vm");

const source = readFileSync(resolve(__dirname, "../../../src/fhir-candle/wwwroot/js/candle-theme.js"), "utf8");
const original = {
    mode: "light", primaryColor: "#123456", neutralColor: "#abc",
    future: { old: true }, removed: "A-only"
};
const payload = mode => JSON.stringify({ ...original, mode });

function browser(json, dark = false, boundary = { denyRead: false, denyWrite: false }) {
    const values = new Map(json === null ? [] : [["theme", json]]);
    const changes = new Set();
    const calls = [];
    const writes = [];
    const document = { documentElement: { dataset: {}, style: {} } };
    const media = {
        matches: dark,
        addEventListener(name, callback) {
            assert.equal(name, "change");
            changes.add(callback);
        },
        removeEventListener(name, callback) {
            assert.equal(name, "change");
            changes.delete(callback);
        }
    };
    const window = {
        matchMedia(query) {
            assert.equal(query, "(prefers-color-scheme: dark)");
            return media;
        },
        localStorage: {
            getItem(key) {
                assert.equal(key, "theme");
                if (boundary.denyRead) throw new Error("read denied");
                return values.get(key) ?? null;
            },
            setItem(key, value) {
                assert.equal(key, "theme");
                if (boundary.denyWrite) throw new Error("write denied");
                writes.push(value);
                values.set(key, value);
            }
        }
    };
    vm.runInNewContext(source, { window, document }, { filename: "candle-theme.js" });
    const callback = { invokeMethodAsync: (...args) => { calls.push(args); return Promise.resolve(); } };
    return {
        adapter: window.candleTheme, document, boundary, changes, calls, writes, values,
        connect: () => window.candleTheme.connect(callback),
        stored: () => JSON.parse(values.get("theme")),
        change(dark) {
            media.matches = dark;
            for (const changed of changes) changed({ matches: dark });
        }
    };
}

test("successive stale-managed writes preserve every latest canonical non-mode member", () => {
    const b = browser(JSON.stringify(original));
    const { id } = b.connect();
    const external = {
        mode: "system", primaryColor: "#654321", neutralColor: "#def",
        future: { nested: [1, { keep: true }] }, added: "B-only"
    };
    b.values.set("theme", JSON.stringify(external));
    for (const [revision, mode] of [[1, "dark"], [2, "light"]]) {
        b.adapter.persist(id, payload(mode), revision);
        assert.deepEqual(b.stored(), { ...external, mode });
    }
    assert.equal(b.writes.length, 2);
});

test("an external empty object remains authoritative across two stale-managed writes", () => {
    const b = browser(JSON.stringify(original));
    const { id } = b.connect();
    b.values.set("theme", "{}");
    for (const [revision, mode] of [[1, "dark"], [2, "light"]]) {
        b.adapter.persist(id, payload(mode), revision);
        assert.deepEqual(b.stored(), { mode });
    }
});

for (const json of [null, "{broken", "null", "[]", "false", "42", '"text"']) {
    test(`unusable canonical storage ${JSON.stringify(json)} falls back to the valid managed object`, () => {
        const b = browser(JSON.stringify(original));
        const { id } = b.connect();
        if (json === null) b.values.delete("theme");
        else b.values.set("theme", json);
        b.adapter.persist(id, payload("dark"), 1);
        assert.deepEqual(b.stored(), { ...original, mode: "dark" });
    });
}

test("invalid managed input cannot replace a valid canonical object's non-mode fields", () => {
    const b = browser(JSON.stringify(original));
    const { id } = b.connect();
    b.adapter.persist(id, "null", 1);
    const { mode, ...nonMode } = original;
    assert.deepEqual(b.stored(), nonMode);
});

test("initialization is read-only and media/apply/disconnect remain isolated", () => {
    const b = browser('{"mode":"system","keep":true}', true);
    assert.equal(b.document.documentElement.dataset.candleTheme, "dark");
    const first = b.connect();
    const second = b.connect();
    assert.equal(first.json, '{"mode":"system","keep":true}');
    assert.equal(first.isSystemDark, true);
    assert.equal(b.writes.length, 0);
    b.change(false);
    assert.deepEqual(b.calls, [["SystemChanged", false], ["SystemChanged", false]]);
    b.adapter.apply(first.id, false);
    assert.equal(b.document.documentElement.style.colorScheme, "light");
    b.adapter.disconnect(first.id);
    b.adapter.disconnect(first.id);
    b.adapter.apply(first.id, true);
    b.adapter.persist(first.id, payload("dark"), 100);
    assert.equal(b.document.documentElement.dataset.candleTheme, "light");
    assert.equal(b.writes.length, 0);
    assert.equal(b.changes.size, 1);
    b.change(true);
    assert.equal(b.calls.length, 3);
    b.adapter.disconnect(second.id);
    assert.equal(b.changes.size, 0);
});

test("missing and invalid initial storage use system appearance without repair writes", () => {
    for (const json of [null, "{broken", "null", "[]", "false"]) {
        const b = browser(json, true);
        const snapshot = b.connect();
        assert.equal(snapshot.json, json);
        assert.equal(b.document.documentElement.dataset.candleTheme, "dark");
        assert.equal(b.writes.length, 0);
        assert.equal(b.values.get("theme") ?? null, json);
    }
    assert.equal(browser('{"mode":"light"}', true).document.documentElement.dataset.candleTheme, "light");
    assert.equal(browser('{"mode":"dark"}', false).document.documentElement.dataset.candleTheme, "dark");
});

test("successful writers reject duplicate/older revisions independently per connection", () => {
    const b = browser(JSON.stringify(original));
    const first = b.connect();
    const second = b.connect();
    assert.notEqual(first.id, second.id);
    b.adapter.persist(first.id, payload("dark"), 5);
    b.adapter.persist(first.id, payload("light"), 5);
    b.adapter.persist(first.id, payload("light"), 4);
    assert.equal(b.writes.length, 1);
    assert.equal(b.stored().mode, "dark");
    b.adapter.persist(second.id, payload("light"), 1);
    assert.equal(b.writes.length, 2);
    assert.equal(b.stored().mode, "light");
});

test("denied reads/writes remain usable, with monotonic revisions even after denial", () => {
    const b = browser(null, false, { denyRead: true, denyWrite: true });
    const { id, json } = b.connect();
    assert.equal(json, null);
    b.adapter.apply(id, true);
    b.adapter.persist(id, payload("dark"), 2);
    assert.equal(b.document.documentElement.dataset.candleTheme, "dark");
    assert.equal(b.writes.length, 0);
    b.boundary.denyWrite = false;
    b.adapter.persist(id, payload("light"), 1);
    b.adapter.persist(id, payload("light"), 2);
    assert.equal(b.writes.length, 0);
    b.adapter.persist(id, payload("light"), 3);
    assert.deepEqual(b.stored(), { ...original, mode: "light" });
    b.boundary.denyRead = false;
    b.adapter.persist(id, payload("dark"), 4);
    assert.deepEqual(b.stored(), { ...original, mode: "dark" });
});
