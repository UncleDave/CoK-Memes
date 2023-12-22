import FormInput from "../core/FormInput.tsx";
import FormTextarea from "../core/FormTextarea.tsx";
import LoreForm from "./LoreForm.tsx";
import MemberLoreContract from "./member-lore-contract.ts";

interface MemberLoreFormProps {
  lore: MemberLoreContract;
}

const MemberLoreForm = ({ lore }: MemberLoreFormProps) => (
  <LoreForm>
    <FormInput label="Name" name="name" defaultValue={lore.name} />
    {/* TODO: Aliases */}
    <FormInput label="Pronouns" name="pronouns" defaultValue={lore.pronouns} />
    <FormInput
      label="Nationality"
      name="nationality"
      defaultValue={lore.nationality}
    />
    {/* TODO: Roles */}
    <FormInput
      label="Main Character"
      name="mainCharacter"
      defaultValue={lore.mainCharacter}
    />
    <FormTextarea
      label="Biography"
      name="biography"
      defaultValue={lore.biography}
    />
  </LoreForm>
);

export default MemberLoreForm;

// TODO: Validation
