import GuildLoreContract from "./guild-lore-contract.ts";

export default interface MemberLoreContract {
  name: string;
  discordUserId?: string | null;
  aliases: string[];
  pronouns: string;
  nationality: string;
  roles: string[];
  mainCharacter: string;
  biography?: string;
}

export const isMemberLore = (
  lore: GuildLoreContract | MemberLoreContract,
): lore is MemberLoreContract => lore && "mainCharacter" in lore;
