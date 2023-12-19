import { useLoaderData } from "react-router-dom";
import LoreContract from "./lore-contract.ts";
import LoreForm from "./LoreForm.tsx";

const EditLorePage = () => {
  const lore = useLoaderData() as LoreContract;

  return (
    <article>
      <h1>{lore.name}</h1>
      <LoreForm lore={lore}/>
    </article>
  );
};

export default EditLorePage;
