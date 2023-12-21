import { css } from "@emotion/react";
import { useLoaderData } from "react-router-dom";
import Panel from "../core/Panel.tsx";
import GuildLoreContract from "./guild-lore-contract.ts";
import GuildLoreForm from "./GuildLoreForm.tsx";
import Breadcrumbs from "../core/Breadcrumbs.tsx";
import MemberLoreContract, { isMemberLore } from "./member-lore-contract.ts";
import MemberLoreForm from "./MemberLoreForm.tsx";

const EditLorePage = () => {
  const lore = useLoaderData() as GuildLoreContract | MemberLoreContract;

  return (
    <article>
      <Breadcrumbs
        crumbs={[
          { label: "Lore", to: "/lore" },
          { label: lore.name },
        ]}
        css={css`
          margin-bottom: 20px;
        `}
      />
      <Panel>
        <h1
          css={css`
            margin-bottom: 10px;
          `}
        >
          {lore.name}
        </h1>
        {isMemberLore(lore) ? (
          <MemberLoreForm lore={lore} />
        ) : (
          <GuildLoreForm lore={lore} />
        )}
      </Panel>
    </article>
  );
};

export default EditLorePage;
