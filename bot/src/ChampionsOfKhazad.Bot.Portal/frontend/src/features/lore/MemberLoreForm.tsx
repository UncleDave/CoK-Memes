import { css } from "@emotion/react";
import { InputHTMLAttributes } from "react";
import Input from "../core/Input.tsx";
import TextArea from "../core/TextArea.tsx";
import LoreForm from "./LoreForm.tsx";
import MemberLoreContract from "./member-lore-contract.ts";

const MemberLoreInput = (props: InputHTMLAttributes<HTMLInputElement>) => (
  <Input
    css={css`
      margin-bottom: 10px;
    `}
    {...props}
  />
);

interface MemberLoreFormProps {
  lore: MemberLoreContract;
}

const MemberLoreForm = ({ lore }: MemberLoreFormProps) => (
  <LoreForm>
    <MemberLoreInput name="name" type="text" defaultValue={lore.name} />
    {/* TODO: Aliases */}
    <MemberLoreInput name="pronouns" type="text" defaultValue={lore.pronouns} />
    <MemberLoreInput
      name="nationality"
      type="text"
      defaultValue={lore.nationality}
    />
    {/* TODO: Roles */}
    <MemberLoreInput
      name="mainCharacter"
      type="text"
      defaultValue={lore.mainCharacter}
    />
    <TextArea name="biography" defaultValue={lore.biography} />
  </LoreForm>
);

export default MemberLoreForm;
