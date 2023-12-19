export default interface MemberLoreContract {
  name: string;
  aliases: string[];
  pronouns: string;
  nationality: string;
  roles: string[];
  mainCharacter: string;
  biography?: string;
}

export const isMemberLore = (lore: any): lore is MemberLoreContract => 'mainCharacter' in lore;
