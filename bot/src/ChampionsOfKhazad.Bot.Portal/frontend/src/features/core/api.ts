const api = {
  update: async (path: string, formData: FormData) =>
    fetch(path, {
      method: "PUT",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(
        Array.from(formData.entries()).reduce<
          Record<string, FormDataEntryValue | FormDataEntryValue[]>
        >((acc, [key, value]) => {
          const existingValue = acc[key];

          if (existingValue) {
            if (Array.isArray(existingValue)) {
              return { ...acc, [key]: [...existingValue, value] };
            }
            return { ...acc, [key]: [existingValue, value] };
          }

          return { ...acc, [key]: value };
        }, {}),
      ),
    }),
};

export default api;
