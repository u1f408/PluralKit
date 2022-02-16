import * as types from 'discord-api-types/v9';
import { ApplicationCommandOptionType } from 'discord-api-types/v9';

const listOptions: types.APIApplicationCommandBasicOption[] = [
    {
        "type": ApplicationCommandOptionType.Integer,
        "name": "sort-by-property",
        "description": "h",
        "choices": [
            { "name": "name", "value": 0 },
            { "name": "id", "value": 1 },
            { "name": "display name", "value": 2 },
            { "name": "creation date", "value": 3 },
            { "name": "last message", "value": 4 },
            { "name": "last switch", "value": 5 },
            { "name": "message count", "value": 6 },
            { "name": "birthday", "value": 7 },
            { "name": "random", "value": 8 }
        ]
    },
    {
        "type": ApplicationCommandOptionType.Integer,
        "name": "show-additional-property",
        "description": "h",
        "choices": [
            { "name": "message count", "value": 0 },
            { "name": "last switch", "value": 1 },
            { "name": "last message", "value": 2 },
            { "name": "creation date", "value": 3 },
            { "name": "avatar url", "value": 4 },
            { "name": "pronouns", "value": 5 },
            { "name": "display name", "value": 6 }
        ]
    },
    {
        "type": ApplicationCommandOptionType.Boolean,
        "name": "search-description",
        "description": "h"
    },
    {
        "type": ApplicationCommandOptionType.Boolean,
        "name": "reverse",
        "description": "h"
    }
];

export default listOptions;