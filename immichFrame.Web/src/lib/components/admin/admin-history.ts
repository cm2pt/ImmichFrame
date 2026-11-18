// Turns the server's change summaries ("Layout: single → splitview", "Account 1: ExcludedAlbums
// changed") into the words the admin pages use ("Layout: One photo → Two portraits",
// "Alex: Never-show albums changed").

const labels: Record<string, string> = {
	Interval: 'Time on each photo',
	TransitionDuration: 'Change between photos',
	Layout: 'Layout',
	Style: 'Behind the clock and captions',
	ImageZoom: 'Slow zoom',
	ImagePan: 'Pan',
	ImageFill: 'Photo size',
	PlayAudio: 'Video sound',
	ShowProgressBar: 'Progress bar',
	PrimaryColor: 'Text color',
	SecondaryColor: 'Box color',
	BaseFontSize: 'Text size',
	Language: 'Language',
	ShowClock: 'Clock',
	ClockFormat: 'Clock time format',
	ClockDateFormat: 'Clock date format',
	ShowPhotoDate: 'Date taken',
	PhotoDateFormat: 'Date taken format',
	ShowImageDesc: 'Description',
	ShowPeopleDesc: 'People on the photo',
	ShowTagsDesc: 'Tags on the photo',
	ShowAlbumName: 'Album name',
	ShowImageLocation: 'Place',
	ImageLocationFormat: 'Place format',
	WeatherApiKey: 'Weather key',
	WeatherLatLong: 'Weather location',
	UnitSystem: 'Units',
	ShowWeatherDescription: 'Weather description',
	WeatherIconUrl: 'Weather icons',
	Webcalendars: 'Calendars',
	DownloadImages: 'Keep images on disk',
	RenewImagesDuration: 'Image cache length',
	RefreshAlbumPeopleInterval: 'Album refresh',
	Webhook: 'Webhook',
	AuthenticationSecret: 'Frame secret',
	AdminPassword: 'Admin password',
	ImmichServerUrl: 'Server address',
	ApiKey: 'API key',
	ApiKeyFile: 'API key file',
	ShowMemories: 'Memories',
	ShowOnThisDayMemories: 'On this day',
	ShowBirthdayMemories: 'Birthdays',
	ShowFavorites: 'Favorites',
	ShowArchived: 'Archived photos',
	ShowVideos: 'Videos',
	ImagesFromDays: 'Taken in the last … days',
	ImagesFromDate: 'Taken from',
	ImagesUntilDate: 'Taken until',
	Albums: 'Albums',
	ExcludedAlbums: 'Never-show albums',
	People: 'People',
	Tags: 'Tags',
	Rating: 'Minimum rating'
};

const values: Record<string, Record<string, string>> = {
	Layout: { single: 'One photo', splitview: 'Two portraits' },
	Style: { blur: 'Frosted', solid: 'Solid', transition: 'Fade', none: 'None' },
	ImageFill: { true: 'Fill screen', false: 'Fit whole photo' },
	UnitSystem: { metric: 'Metric', imperial: 'Imperial' }
};

function value(key: string, raw: string): string {
	if (raw === '(empty)') return 'default';
	const mapped = values[key]?.[raw];
	if (mapped) return mapped;
	if (raw === 'true') return 'on';
	if (raw === 'false') return 'off';
	if (key === 'Interval' || key === 'TransitionDuration') return `${raw} s`;
	if (/^\d{4}-\d{2}-\d{2}T/.test(raw)) return raw.slice(0, 10);
	return raw;
}

export function humanizeChange(line: string, accountNames: string[]): string {
	const restored = /^Restored version (\d+)$/.exec(line);
	if (restored) return `Went back to an earlier version`;
	if (line === 'First settings') return 'First setup';
	if (line === 'Oldest kept version') return 'Oldest saved version';

	let prefix = '';
	let rest = line;
	const account = /^Account (\d+)(: | added| removed)(.*)$/.exec(line);
	if (account) {
		const name = accountNames[Number(account[1]) - 1] || `Account ${account[1]}`;
		if (account[2] === ' added') return `${name} added`;
		if (account[2] === ' removed') return `${name} removed`;
		prefix = `${name}: `;
		rest = account[3];
	}

	const arrow = /^(\w+): (.*) → (.*)$/.exec(rest);
	if (arrow) {
		const [, key, from, to] = arrow;
		return `${prefix}${labels[key] ?? key}: ${value(key, from)} → ${value(key, to)}`;
	}
	const verb = /^(\w+) (changed|set|removed)$/.exec(rest);
	if (verb) {
		const [, key, what] = verb;
		return `${prefix}${labels[key] ?? key} ${what === 'set' ? 'added' : what}`;
	}
	return line;
}
