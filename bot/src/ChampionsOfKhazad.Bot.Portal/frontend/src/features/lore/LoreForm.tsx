import { Form } from "react-router-dom";
import LoreContract from "./lore-contract.ts";

interface LoreFormProps {
  lore: LoreContract;
}

const LoreForm = ({ lore }: LoreFormProps) => (
  <Form>
    <textarea>{lore.content}</textarea>
  </Form>
);

export default LoreForm;
