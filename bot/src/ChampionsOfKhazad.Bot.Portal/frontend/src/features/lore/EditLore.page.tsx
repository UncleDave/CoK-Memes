import { useLoaderData } from "react-router-dom";
import LoreContract from "./lore-contract.ts";

const EditLorePage = () => {
  const lore = useLoaderData() as LoreContract;

  return (
    <article>
      <h1>{lore.name}</h1>
      <textarea>{lore.content}</textarea>
    </article>
  );
};

export default EditLorePage;
