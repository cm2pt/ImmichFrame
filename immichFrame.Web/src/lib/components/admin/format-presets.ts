import { format } from 'date-fns';
import * as locales from 'date-fns/locale';
import { SvelteMap, SvelteSet } from 'svelte/reactivity';

// A fixed sample moment. Day 25 can't be a month, so 25/12 and 12/25 read as orders, not as
// two dates; 14:30 tells 24-hour from 12-hour. Each option also says which it is (formatHint).
export const SAMPLE_DATE = new Date(2025, 11, 25, 14, 30, 5);

export const CUSTOM_FORMAT = '__custom__';

/**
 * Keys of format fields whose Custom box holds an invalid pattern. Save is blocked while it's
 * not empty: the settings keep the last valid value, which may be a half-typed prefix (H of HC).
 */
export const invalidFormats = new SvelteSet<string>();

/**
 * The invalid drafts themselves, by key. They outlive the field (a collapsed section unmounts
 * it), so reopening shows the draft again and Save stays blocked until it's fixed.
 */
export const formatDrafts = new SvelteMap<string, string>();

/** Forget unfinished drafts, e.g. when the settings are (re)loaded. */
export function clearFormatDrafts() {
	formatDrafts.clear();
	invalidFormats.clear();
}

/**
 * Renders a date-fns pattern the way the frame will, or returns null when the pattern is invalid.
 * `localized` mirrors the slideshow: dates use the configured language, the clock time doesn't.
 */
export function formatExample(
	pattern: string,
	language: unknown,
	localized: boolean,
	date: Date = SAMPLE_DATE
): string | null {
	const locale = localized
		? (locales[language as keyof typeof locales] as locales.Locale | undefined)
		: undefined;
	try {
		return format(date, pattern, { locale: locale ?? locales.enUS });
	} catch {
		return null;
	}
}

/**
 * What a pattern's example can't say by itself: 24- or 12-hour, and which comes first in a
 * numeric date. Empty when the example is plain (a written-out month, "December 2025").
 */
export function formatHint(pattern: string): string {
	const literal = pattern.replace(/'[^']*'/g, '');
	if (/[Hh]/.test(literal)) {
		if (/H/.test(literal)) return '24-hour';
		return /a/.test(literal) ? '12-hour' : '12-hour, no AM/PM';
	}
	if (/M{3,}/.test(literal)) return '';
	const day = literal.search(/d/);
	const month = literal.search(/M/);
	const year = literal.search(/y/);
	if (day < 0 || month < 0) return '';
	if (year >= 0 && year < month && year < day) return 'Year first';
	return day < month ? 'Day first' : 'Month first';
}
