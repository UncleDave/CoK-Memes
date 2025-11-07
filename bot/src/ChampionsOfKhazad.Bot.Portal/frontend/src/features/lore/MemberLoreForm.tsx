import FormInput from "../core/FormInput.tsx";
import FormMultiInput from "../core/FormMultiInput.tsx";
import FormTextarea from "../core/FormTextarea.tsx";
import LoreForm from "./LoreForm.tsx";
import MemberLoreContract from "./member-lore-contract.ts";

interface MemberLoreFormProps {
  lore: MemberLoreContract;
  isCreating?: boolean;
}

const MemberLoreForm = ({ lore, isCreating = false }: MemberLoreFormProps) => (
  <LoreForm>
    <FormInput
      label="Name"
      name="name"
      defaultValue={isCreating ? "" : lore.name}
      required
      disabled={!isCreating}
    />
    <FormMultiInput
      label="Aliases"
      name="aliases"
      defaultValues={lore.aliases}
    />
    <FormInput
      label="Pronouns"
      name="pronouns"
      defaultValue={lore.pronouns}
      required
    />
    <FormInput
      label="Nationality"
      name="nationality"
      defaultValue={lore.nationality}
      required
    />
    <FormMultiInput label="Roles" name="roles" defaultValues={lore.roles} />
    <FormInput
      label="Main Character"
      name="mainCharacter"
      defaultValue={lore.mainCharacter}
      required
    />
    <FormTextarea
      label="Biography"
      name="biography"
      defaultValue={lore.biography}
    />
  </LoreForm>
);

export default MemberLoreForm;
