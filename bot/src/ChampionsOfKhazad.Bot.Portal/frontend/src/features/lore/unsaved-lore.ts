export const getLoreFormSnapshot = (formData: FormData): string =>
  JSON.stringify(Array.from(formData.entries()));

interface FormLocation {
  pathname: string;
  search: string;
}

export const shouldBlockLoreNavigation = (
  isDirty: boolean,
  currentLocation: FormLocation,
  nextLocation: FormLocation,
): boolean =>
  isDirty &&
  (currentLocation.pathname !== nextLocation.pathname ||
    currentLocation.search !== nextLocation.search);
