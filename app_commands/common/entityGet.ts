import * as types from 'discord-api-types/v9';
import { ApplicationCommandOptionType, ApplicationCommandType } from 'discord-api-types/v9';

import listOptions from './list';

// entity get/list base command that should work for members and groups

export default (entityName: string): types.RESTPutAPIApplicationCommandsJSONBody => [{
    "type": ApplicationCommandType.ChatInput,
    "name": entityName,
    "description": "h",
    "options": [
        {
            "type": ApplicationCommandOptionType.Subcommand,
            "name": "list",
            "description": "h",
            "options": listOptions
        },
        {
            "type": ApplicationCommandOptionType.Subcommand,
            "name": "show",
            "description": "h",
            "options": [
                {
                    "type": ApplicationCommandOptionType.String,
                    "name": entityName,
                    "description": "h",
                    "required": true,
                    "autocomplete": true
                },
                {
                    "type": ApplicationCommandOptionType.String,
                    "name": "private",
                    "description": "h"
                }
            ]
        }
    ]
}];
