import { Client as RestClient } from 'detritus-client-rest';
import commands from './commands';

import fs from 'fs';

const config = JSON.parse(fs.readFileSync("./pluralkit.conf").toString('utf-8')).PluralKit;

const rest = new RestClient(config.Bot.Token);

const guildId = "865783801632653372";

// @ts-ignore
rest.bulkOverwriteApplicationGuildCommands(config.Bot.ClientId, guildId, commands)
    .then(() => console.log("Successfully updated application commands on Discord's end"))
    .catch(e => console.error("Failed to update application commands:", e));
