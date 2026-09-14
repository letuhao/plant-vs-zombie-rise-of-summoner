export type RiftAssetRole = "storySprite" | "icon";

export type RiftAsset = {
  role: RiftAssetRole;
  src: string;
  alt: string;
  fallbackLabel: string;
};

/** Versioned, filename-independent roles for the prologue art contract. */
export const RIFT_ASSETS = {
  storySprite: {
    role: "storySprite",
    src: "/assets/onboarding/rift-portal-pvz-style.png",
    alt: "A bright purple tear edged with lime energy",
    fallbackLabel: "Rift visual unavailable"
  },
  icon: {
    role: "icon",
    src: "/assets/onboarding/rift-icon-pvz-style-64.png",
    alt: "Small Rift marker",
    fallbackLabel: "Rift marker unavailable"
  }
} as const satisfies Record<RiftAssetRole, RiftAsset>;

export const RIFT_ASSET_VERSION = 1;
