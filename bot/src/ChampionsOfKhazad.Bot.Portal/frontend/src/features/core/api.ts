import requestApi from "./request-api.ts";

const buildGuildLoreBody = (formData: FormData, includeName = false) => ({
  ...(includeName && { name: formData.get("name") }),
  content: formData.get("content"),
});

const buildMemberLoreBody = (formData: FormData, includeName = false) => ({
  ...(includeName && { name: formData.get("name") }),
  pronouns: formData.get("pronouns"),
  nationality: formData.get("nationality"),
  mainCharacter: formData.get("mainCharacter"),
  biography: formData.get("biography"),
  aliases: formData.getAll("aliases"),
  roles: formData.getAll("roles"),
});

const api = {
  createGuildLore: async (formData: FormData) =>
    requestApi("/api/guild-lore", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(buildGuildLoreBody(formData, true)),
    }),
  updateGuildLore: async (name: string, formData: FormData) =>
    requestApi(`/api/guild-lore/${encodeURIComponent(name)}`, {
      method: "PUT",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(buildGuildLoreBody(formData)),
    }),
  createMemberLore: async (formData: FormData) =>
    requestApi("/api/member-lore", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(buildMemberLoreBody(formData, true)),
    }),
  updateMemberLore: async (name: string, formData: FormData) =>
    requestApi(`/api/member-lore/${encodeURIComponent(name)}`, {
      method: "PUT",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(buildMemberLoreBody(formData)),
    }),
  deleteLore: async (name: string) =>
    requestApi(`/api/lore/${encodeURIComponent(name)}`, {
      method: "DELETE",
    }),
};

export default api;
