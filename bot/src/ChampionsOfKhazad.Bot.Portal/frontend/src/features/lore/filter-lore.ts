const filterLore = <T extends { name: string }>(
  lore: T[],
  query: string,
): T[] => {
  const search = query.trim().toLowerCase();
  return search
    ? lore.filter((entry) => entry.name.toLowerCase().includes(search))
    : lore;
};

export default filterLore;
