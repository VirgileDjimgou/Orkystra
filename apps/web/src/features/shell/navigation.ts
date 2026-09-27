export type NavigationItem = {
  label: string;
  to: string;
  icon: string;
  adminOnly?: boolean;
};

export type NavigationGroup = {
  label: "Operate" | "Fleet" | "Manage" | "Administration";
  items: NavigationItem[];
};

export const primaryNavigation: NavigationGroup[] = [
  {
    label: "Operate",
    items: [
      { label: "Operations cockpit", to: "/", icon: "01" },
      { label: "Exception queue", to: "/operations", icon: "!" },
    ],
  },
  {
    label: "Fleet",
    items: [
      { label: "Vehicles", to: "/fleet/vehicles", icon: "V" },
      { label: "Drivers", to: "/fleet/drivers", icon: "D" },
      { label: "Devices", to: "/fleet/devices", icon: "G" },
    ],
  },
  {
    label: "Manage",
    items: [
      { label: "Missions", to: "/dispatch/missions", icon: "M" },
      { label: "Daily planning", to: "/dispatch/productivity", icon: "P" },
      { label: "Maintenance", to: "/maintenance", icon: "W" },
      { label: "Compliance", to: "/compliance", icon: "C" },
    ],
  },
  {
    label: "Administration",
    items: [
      {
        label: "Guided setup",
        to: "/admin/onboarding",
        icon: "✓",
        adminOnly: true,
      },
      { label: "Pilot review", to: "/admin/pilot", icon: "α", adminOnly: true },
      { label: "Users", to: "/admin/users", icon: "U", adminOnly: true },
      {
        label: "Security & data",
        to: "/admin/security",
        icon: "S",
        adminOnly: true,
      },
      {
        label: "Integrations",
        to: "/admin/integrations",
        icon: "I",
        adminOnly: true,
      },
    ],
  },
];
