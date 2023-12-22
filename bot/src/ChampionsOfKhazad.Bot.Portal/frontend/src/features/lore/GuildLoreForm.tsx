import FormTextarea from '../core/FormTextarea.tsx';
import GuildLoreContract from "./guild-lore-contract.ts";
import LoreForm from "./LoreForm.tsx";

interface GuildLoreFormProps {
  lore: GuildLoreContract;
}

const GuildLoreForm = ({ lore }: GuildLoreFormProps) => (
  <LoreForm>
    <FormTextarea label="Content" name="content" defaultValue={lore.content} />
  </LoreForm>
);

export default GuildLoreForm;

// TODO: Validation
