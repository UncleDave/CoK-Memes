import { useLoaderData } from "react-router";
import GuildLoreContract from "./guild-lore-contract.ts";
import GuildLoreForm from "./GuildLoreForm.tsx";
import MemberLoreContract, { isMemberLore } from "./member-lore-contract.ts";
import MemberLoreForm from "./MemberLoreForm.tsx";
import Page from "../core/Page.tsx";

const EditLorePage = () => {
  const lore = useLoaderData() as GuildLoreContract | MemberLoreContract;

  return (
    <Page
      title={lore.name}
      crumbs={[{ label: "Lore", to: "/lore" }, { label: lore.name }]}
    >
      {isMemberLore(lore) ? (
        <MemberLoreForm lore={lore} />
      ) : (
        <GuildLoreForm lore={lore} />
      )}
    </Page>
  );
};

export default EditLorePage;
