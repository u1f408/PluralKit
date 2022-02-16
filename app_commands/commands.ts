import * as types from 'discord-api-types/v9';
import { ApplicationCommandType } from 'discord-api-types/v9';

import entityGet from './common/entityGet';

const commands: types.RESTPutAPIApplicationCommandsJSONBody = [
    ...entityGet("member"),
    ...entityGet("group"),
    {
        "type": ApplicationCommandType.Message,
        "name": "Edit Message"
    },
    {
        "type": ApplicationCommandType.Message,
        "name": "Lookup Message Info"
    },
    {
        "type": ApplicationCommandType.Message,
        "name": "Delete message"
    }
]

export default commands;
