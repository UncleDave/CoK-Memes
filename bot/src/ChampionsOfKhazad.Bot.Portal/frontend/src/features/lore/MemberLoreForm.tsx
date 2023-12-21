import LoreForm from "./LoreForm.tsx";
import MemberLoreContract from "./member-lore-contract.ts";

interface MemberLoreFormProps {
  lore: MemberLoreContract;
}

const MemberLoreForm = ({ lore }: MemberLoreFormProps) => (
  <LoreForm>
    <input name="name" type="text" defaultValue={lore.name} />
  </LoreForm>
);

export default MemberLoreForm;
