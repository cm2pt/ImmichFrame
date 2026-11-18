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

	async load(count = 4) {
		this.state = 'loading';
		const previous = this.items;
		try {
			const res = await adminApi.getPreview(count);
			if (res.status !== 200) throw new Error();
			const items: PreviewItem[] = await Promise.all(
				res.data.map(async (asset) => ({
					...asset,
					url: asset.id ? await adminApi.getPreviewImageUrl(asset.id).catch(() => null) : null
				}))
			);
			this.items = items;
			this.state = 'ready';
		} catch {
			this.state = 'error';
			this.message = 'No sample: check that an account is connected and saved.';
		} finally {
			previous.forEach((item) => item.url && URL.revokeObjectURL(item.url));
		}
	}
}
