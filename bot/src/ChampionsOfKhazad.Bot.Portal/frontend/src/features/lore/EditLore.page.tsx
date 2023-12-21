import { css } from "@emotion/react";
import { useLoaderData } from "react-router-dom";
import Panel from "../core/Panel.tsx";
import LoreContract from "./lore-contract.ts";
import LoreForm from "./LoreForm.tsx";
import Breadcrumbs from "../core/Breadcrumbs.tsx";

const EditLorePage = () => {
  const lore = useLoaderData() as LoreContract;

  return (
    <article>
      <Breadcrumbs
        crumbs={[
          { label: "Lore", to: "/lore" },
          { label: lore.name, to: "." },
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
        <LoreForm lore={lore} />
      </Panel>
    </article>
  );
};

export default EditLorePage;
