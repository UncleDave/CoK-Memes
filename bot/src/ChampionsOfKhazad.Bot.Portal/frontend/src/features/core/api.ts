const api = {
  updateGuildLore: async (name: string, formData: FormData) =>
    fetch(`/api/guild-lore/${name}`, {
      method: "PUT",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        content: formData.get("content"),
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
