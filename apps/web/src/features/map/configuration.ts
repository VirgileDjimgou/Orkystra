export type MapTileProviderConfiguration = {
  url: string;
  attribution: string;
  maxZoom: number;
};

const devProvider: MapTileProviderConfiguration = {
  url: "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
  attribution: "&copy; OpenStreetMap contributors",
  maxZoom: 19,
};

export function getMapTileProviderConfiguration(): MapTileProviderConfiguration {
  const url = import.meta.env.VITE_MAP_TILE_URL?.trim();
  const attribution = import.meta.env.VITE_MAP_TILE_ATTRIBUTION?.trim();
  return url && attribution
    ? {
        url,
        attribution,
        maxZoom: Number(import.meta.env.VITE_MAP_TILE_MAX_ZOOM) || 19,
      }
    : devProvider;
}
