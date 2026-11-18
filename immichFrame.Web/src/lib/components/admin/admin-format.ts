/** A rough device guess from a user agent, for the Screens page. */
export function describeDevice(userAgent: string | null | undefined): {
	kind: string;
	label: string;
} {
	const ua = userAgent ?? '';
	if (/AppleTV|tvOS/i.test(ua)) return { kind: 'tv', label: 'Apple TV' };
	if (/CrKey|GoogleTV|Android TV|AFT|BRAVIA|SMART-TV|Tizen|Web0S/i.test(ua))
		return { kind: 'tv', label: 'TV' };
	if (/iPad/i.test(ua)) return { kind: 'tablet', label: 'iPad' };
	if (/iPhone/i.test(ua)) return { kind: 'phone', label: 'iPhone' };
	if (/Frameo/i.test(ua)) return { kind: 'tablet', label: 'Frameo' };
	if (/Android/i.test(ua))
		return /Mobile/i.test(ua)
			? { kind: 'phone', label: 'Android phone' }
			: { kind: 'tablet', label: 'Android tablet' };
	if (/Macintosh/i.test(ua)) return { kind: 'desktop', label: 'Mac' };
	if (/Windows/i.test(ua)) return { kind: 'desktop', label: 'Windows' };
	if (/Linux/i.test(ua)) return { kind: 'desktop', label: 'Linux' };
	return { kind: 'desktop', label: ua ? 'Browser' : 'Unknown device' };
}

const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

export function relativeTime(then: number, now = Date.now()): string {
	const seconds = Math.round((then - now) / 1000);
	const abs = Math.abs(seconds);
	if (abs < 60) return rtf.format(seconds, 'second');
	if (abs < 3600) return rtf.format(Math.round(seconds / 60), 'minute');
	if (abs < 86400) return rtf.format(Math.round(seconds / 3600), 'hour');
	return rtf.format(Math.round(seconds / 86400), 'day');
}
