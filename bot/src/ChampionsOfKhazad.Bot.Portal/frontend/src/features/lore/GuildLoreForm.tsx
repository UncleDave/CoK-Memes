import TextArea from "../core/TextArea.tsx";
import GuildLoreContract from "./guild-lore-contract.ts";
import LoreForm from "./LoreForm.tsx";

interface GuildLoreFormProps {
  lore: GuildLoreContract;
}

const GuildLoreForm = ({ lore }: GuildLoreFormProps) => (
  <LoreForm>
    <TextArea name="content" defaultValue={lore.content} />
  </LoreForm>
);

export default GuildLoreForm;
