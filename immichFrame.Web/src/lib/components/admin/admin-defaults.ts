import type { GeneralSettings, ServerAccountSettings } from '$lib/immichFrameApi';

// The server's defaults (ImmichFrame.WebApi/Models/ServerSettings.cs). The stored document
// only holds what was set, so the editor shows these for anything missing.
export const generalDefaults: GeneralSettings = {
	downloadImages: false,
	language: 'en',
	imageLocationFormat: 'City,State,Country',
	photoDateFormat: 'MM/dd/yyyy',
	interval: 45,
	transitionDuration: 1,
	showClock: true,
	clockFormat: 'hh:mm',
	clockDateFormat: 'eee, MMM d',
	showProgressBar: true,
	showPhotoDate: true,
	showImageDesc: true,
	showPeopleDesc: true,
	showPeopleAge: false,
	showTagsDesc: true,
	showAlbumName: true,
	showImageLocation: true,
	style: 'none',
	showWeatherDescription: true,
	weatherIconUrl: 'https://openweathermap.org/img/wn/{IconId}.png',
	imageZoom: true,
	imagePan: false,
	imageFill: false,
	playAudio: false,
	layout: 'splitview',
	renewImagesDuration: 30,
	webcalendars: [],
	calendarDaysAhead: 1,
	slideshowOrder: 'random',
	refreshAlbumPeopleInterval: 12,
	unitSystem: 'imperial'
};

export const accountDefaults: ServerAccountSettings = {
	showMemories: false,
	showOnThisDayMemories: true,
	showBirthdayMemories: true,
	showFavorites: false,
	showArchived: false,
	showVideos: false,
	albums: [],
	excludedAlbums: [],
	people: [],
	tags: [],
	excludedPeople: [],
	excludedTags: []
};

/** The effective value: what's stored, or the server default when it isn't set. */
export function effective<T extends object, K extends keyof T>(
	target: T,
	defaults: T,
	key: K
): T[K] {
	const value = target[key];
	return value === null || value === undefined || (value as unknown) === '' ? defaults[key] : value;
}

/** Fields that hold secrets: never in downloads, never shown in history values. */
export const generalSecrets = ['weatherApiKey', 'authenticationSecret', 'adminPassword'] as const;
export const accountSecrets = ['apiKey'] as const;
