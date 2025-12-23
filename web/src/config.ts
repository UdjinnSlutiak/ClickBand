const INTERNAL_MARKERS = ['clickband-api', 'clickband-web'];

function isInternalUrl(url?: string | null): boolean {
  if (!url) return true;
  return INTERNAL_MARKERS.some((marker) => url.includes(marker));
}

export function resolveApiBaseUrl(): string {
  const envBase = import.meta.env.VITE_API_BASE_URL;
  if (envBase && !isInternalUrl(envBase)) {
    return envBase.replace(/\/$/, '');
  }
  return '/api';
}

export function resolveHubUrl(): string {
  const envHub = import.meta.env.VITE_SIGNALR_HUB_URL;
  if (envHub && !isInternalUrl(envHub)) {
    return envHub;
  }

  if (typeof window !== 'undefined') {
    return `${window.location.origin.replace(/\/$/, '')}/hubs/rooms`;
  }

  return '/hubs/rooms';
}
