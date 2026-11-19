<script lang="ts">
	import * as api from '$lib/index';
	import { onMount } from 'svelte';
	import { format, isSameDay, isToday } from 'date-fns';
	import { configStore } from '$lib/stores/config.store';
	import { clientIdentifierStore } from '$lib/stores/persist.store';

	api.init();

	function formatDates(startTime: string, endTime: string) {
		const startDate = new Date(startTime);
		const endDate = new Date(endTime);

		const clockFormat = $configStore.clockFormat ?? 'HH:mm';
		const clockDateFormat = $configStore.clockDateFormat ?? 'eee, MMM d';
		const fullFormat = clockDateFormat + ' ' + clockFormat;

		if (isSameDay(startDate, endDate)) {
			const times = format(startDate, clockFormat) + ' - ' + format(endDate, clockFormat);
			// With CalendarDaysAhead above 1, an event on another day says which day.
			return isToday(startDate) ? times : format(startDate, clockDateFormat) + ' · ' + times;
		}

		return format(startDate, fullFormat) + ' - ' + format(endDate, fullFormat);
	}

	let appointments: api.IAppointment[] = $state() as api.IAppointment[];

	onMount(() => {
		GetAppointments();
		const appointmentInterval = setInterval(() => GetAppointments(), 10 * 60 * 1000); //every 10 minutes

		return () => {
			clearInterval(appointmentInterval);
		};
	});

	async function GetAppointments() {
		let appointmentRequest = await api.getAppointments({
			clientIdentifier: $clientIdentifierStore
		});
		if (appointmentRequest.status == 200) {
			appointments = appointmentRequest.data;

			appointments = appointmentRequest.data.sort((a, b) => {
				return new Date(a.startTime ?? '').getTime() - new Date(b.startTime ?? '').getTime();
			});
		}
	}
</script>

{#if appointments}
	<div
		id="appointments"
		class="fixed top-0 right-0 w-auto z-10 text-center text-frame-primary m-5 max-w-[20%] hidden lg:block md:min-w-[10%]"
	>
		<!-- <div class="text-4xl mx-8 font-bold">Appointments</div> -->
		<div class="">
			{#each appointments as appointment}
				<div class="bg-gray-600/90 mb-2 text-left rounded-md p-3">
					<p class="text-xs">
						{formatDates(appointment.startTime ?? '', appointment.endTime ?? '')}
					</p>
					{appointment.summary}
					{#if appointment.description}
						<p class="text-xs font-light">{appointment.description}</p>
					{/if}
				</div>
			{/each}
		</div>
	</div>
{/if}
