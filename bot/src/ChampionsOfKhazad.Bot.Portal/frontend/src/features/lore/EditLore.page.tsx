import { css } from "@emotion/react";
import { useLoaderData } from "react-router-dom";
import GuildLoreContract from "./guild-lore-contract.ts";
import GuildLoreForm from "./GuildLoreForm.tsx";
import Breadcrumbs from "../core/Breadcrumbs.tsx";
import MemberLoreContract, { isMemberLore } from "./member-lore-contract.ts";
import MemberLoreForm from "./MemberLoreForm.tsx";
import { Sheet, Typography } from "@mui/joy";

const EditLorePage = () => {
  const lore = useLoaderData() as GuildLoreContract | MemberLoreContract;

  return (
    <article>
      <Breadcrumbs
        crumbs={[{ label: "Lore", to: "/lore" }, { label: lore.name }]}
      />
      <Sheet
        color="primary"
        variant="outlined"
        css={css`
          padding: 10px;
        `}
      >
        <Typography
          level="h1"
          css={css`
            margin-bottom: 10px;
          `}
        >
          {lore.name}
        </Typography>
        {isMemberLore(lore) ? (
          <MemberLoreForm lore={lore} />
        ) : (
          <GuildLoreForm lore={lore} />
        )}
      </Sheet>
    </article>
  );
};

export default EditLorePage;
