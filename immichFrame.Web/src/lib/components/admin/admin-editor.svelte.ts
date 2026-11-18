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
