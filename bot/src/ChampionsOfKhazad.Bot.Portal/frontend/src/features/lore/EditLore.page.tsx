import { css } from "@emotion/react";
import { useLoaderData } from "react-router-dom";
import Panel from "../core/Panel.tsx";
import LoreContract from "./lore-contract.ts";
import LoreForm from "./LoreForm.tsx";

const EditLorePage = () => {
  const lore = useLoaderData() as LoreContract;

  return (
    <article>
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
