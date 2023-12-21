const api = {
  update: async (path: string, data: Request) =>
    fetch(path, {
      method: "PUT",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(Object.fromEntries(await data.formData())),
    }),
};

export default api;
