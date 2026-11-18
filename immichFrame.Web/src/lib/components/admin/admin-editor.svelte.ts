import type { ServerSettings } from '$lib/immichFrameApi';

/**
 * The settings being edited, plus the last saved copy. Dirty is a structural comparison,
 * so undoing a change by hand also clears the unsaved-changes bar.
 */
export class AdminEditor {
	settings: ServerSettings = $state({ General: {}, Accounts: [] });
	saved = $state('{}');
	/** Bumped whenever the document is replaced, so editors with local state remount. */
	version = $state(0);

	dirty = $derived(normalize(this.settings) !== this.saved);

	/** How many individual settings differ from the saved copy (for "3 unsaved changes"). */
	changeCount = $derived.by(() => {
		if (!this.dirty) return 0;
		const before = flatten(JSON.parse(this.saved));
		const after = flatten(JSON.parse(normalize(this.settings)));
		const keys = [...Object.keys(before), ...Object.keys(after)].filter(
			(k, i, all) => all.indexOf(k) === i
		);
		return keys.filter((key) => before[key] !== after[key]).length;
	});

	load(settings: ServerSettings) {
		const copy: ServerSettings = {
			General: settings.General ?? {},
			Accounts: settings.Accounts ?? []
		};
		this.settings = structuredClone(copy);
		this.saved = normalize(copy);
		this.version++;
	}

	discard() {
		this.load(JSON.parse(this.saved));
	}

	markSaved() {
		this.saved = normalize(this.settings);
	}

	snapshot(): ServerSettings {
		return JSON.parse(JSON.stringify(this.settings));
	}
}

// Empty values mean "server default", the same as a missing key (see services/admin.ts clean).
function normalize(settings: ServerSettings): string {
	return JSON.stringify(settings, (_key, value) => {
		if (value && typeof value === 'object' && !Array.isArray(value)) {
			const entries = Object.entries(value)
				.filter(([, v]) => v !== null && v !== undefined && v !== '')
				.sort(([a], [b]) => a.localeCompare(b));
			return Object.fromEntries(entries);
		}
		return value;
	});
}

// General.layout, Accounts.0.albums, …: one entry per setting, with arrays compared whole.
function flatten(settings: ServerSettings): Record<string, string> {
	const out: Record<string, string> = {};
	for (const [key, value] of Object.entries(settings.General ?? {})) {
		out[`General.${key}`] = JSON.stringify(value);
	}
	const accounts = settings.Accounts ?? [];
	out['Accounts.length'] = String(accounts.length);
	accounts.forEach((account, i) => {
		for (const [key, value] of Object.entries(account)) {
			out[`Accounts.${i}.${key}`] = JSON.stringify(value);
		}
	});
	return out;
}
