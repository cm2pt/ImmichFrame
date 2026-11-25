import type { AdminPreviewAssetDto } from '$lib/immichFrameApi';
import * as adminApi from '$lib/services/admin';

export interface PreviewItem extends AdminPreviewAssetDto {
	url: string | null;
}

/** A small sample from the frame's real pool, shared by the live preview and "Up next". */
export class AdminPreview {
	items: PreviewItem[] = $state([]);
	state: 'idle' | 'loading' | 'ready' | 'error' = $state('idle');
	message = $state('');

	private run = 0;

	async load(count = 4) {
		// Only the latest load may show its photos; the old ones stay up until it has replaced
		// them (a failed refresh keeps them, rather than leaving broken images).
		const run = ++this.run;
		this.state = 'loading';
		let items: PreviewItem[] = [];
		try {
			const res = await adminApi.getPreview(count);
			if (res.status !== 200) throw new Error();
			items = await Promise.all(
				res.data.map(async (asset) => ({
					...asset,
					url: asset.id ? await adminApi.getPreviewImageUrl(asset.id).catch(() => null) : null
				}))
			);
		} catch {
			if (run !== this.run) return;
			this.state = 'error';
			this.message = 'No sample: check that an account is connected and saved.';
			return;
		}
		if (run !== this.run) {
			revoke(items);
			return;
		}
		const previous = this.items;
		this.items = items;
		this.state = 'ready';
		revoke(previous);
	}
}

function revoke(items: PreviewItem[]) {
	items.forEach((item) => item.url && URL.revokeObjectURL(item.url));
}
