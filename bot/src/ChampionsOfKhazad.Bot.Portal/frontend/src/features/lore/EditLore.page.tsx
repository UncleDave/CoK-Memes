import { useLoaderData, useSearchParams } from "react-router";
import GuildLoreContract from "./guild-lore-contract.ts";
import GuildLoreForm from "./GuildLoreForm.tsx";
import MemberLoreContract, { isMemberLore } from "./member-lore-contract.ts";
import MemberLoreForm from "./MemberLoreForm.tsx";
import Page from "../core/Page.tsx";

const EditLorePage = () => {
  const lore = useLoaderData() as GuildLoreContract | MemberLoreContract | null;
  const [searchParams] = useSearchParams();
  const type = searchParams.get("type");
  const isCreating = !lore;

  // For creation, create an empty lore object based on type
  const loreData: GuildLoreContract | MemberLoreContract = isCreating
    ? type === "member"
      ? {
          name: "",
          aliases: [],
          pronouns: "",
          nationality: "",
          roles: [],
          mainCharacter: "",
          biography: "",
        }
      : { name: "", content: "" }
    : lore;

  const loreName = isCreating
    ? type === "member"
      ? "New Member Lore"
      : "New Guild Lore"
    : lore.name;

  const isMember = isCreating
    ? type === "member"
    : isMemberLore(loreData as GuildLoreContract | MemberLoreContract);

  return (
    <Page
      title={loreName}
      crumbs={[{ label: "Lore", to: "/lore" }, { label: loreName }]}
    >
      {isMember ? (
        <MemberLoreForm
          lore={loreData as MemberLoreContract}
          isCreating={isCreating}
        />
      ) : (
        <GuildLoreForm
          lore={loreData as GuildLoreContract}
          isCreating={isCreating}
        />
      )}
    </Page>
  );
};

export default EditLorePage;
