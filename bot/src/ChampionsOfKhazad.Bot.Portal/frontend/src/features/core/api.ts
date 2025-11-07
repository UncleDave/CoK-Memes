const api = {
  createGuildLore: async (formData: FormData) =>
    fetch("/api/guild-lore", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        name: formData.get("name"),
        content: formData.get("content"),
      }),
    }),
  updateGuildLore: async (name: string, formData: FormData) =>
    fetch(`/api/guild-lore/${name}`, {
      method: "PUT",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        content: formData.get("content"),
      }),
    }),
  createMemberLore: async (formData: FormData) =>
    fetch("/api/member-lore", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        name: formData.get("name"),
        pronouns: formData.get("pronouns"),
        nationality: formData.get("nationality"),
        mainCharacter: formData.get("mainCharacter"),
        biography: formData.get("biography"),
        aliases: formData.getAll("aliases"),
        roles: formData.getAll("roles"),
      }),
    }),
  updateMemberLore: async (name: string, formData: FormData) =>
    fetch(`/api/member-lore/${name}`, {
      method: "PUT",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        pronouns: formData.get("pronouns"),
        nationality: formData.get("nationality"),
        mainCharacter: formData.get("mainCharacter"),
        biography: formData.get("biography"),
        aliases: formData.getAll("aliases"),
        roles: formData.getAll("roles"),
      }),
    }),
};

export default api;
