import FormInput from "../core/FormInput.tsx";
import FormTextarea from "../core/FormTextarea.tsx";
import GuildLoreContract from "./guild-lore-contract.ts";
import LoreForm from "./LoreForm.tsx";

interface GuildLoreFormProps {
  lore: GuildLoreContract;
  isCreating?: boolean;
}

const GuildLoreForm = ({ lore, isCreating = false }: GuildLoreFormProps) => (
  <LoreForm>
    <FormInput
      label="Name"
      name="name"
      defaultValue={isCreating ? "" : lore.name}
      required
      disabled={!isCreating}
    />
    <FormTextarea
      label="Content"
      name="content"
      defaultValue={lore.content}
      required
    />
  </LoreForm>
);

export default GuildLoreForm;
