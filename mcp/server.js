import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
    CallToolRequestSchema,
    ListToolsRequestSchema,
} from '@modelcontextprotocol/sdk/types.js';
import { execFile } from 'child_process';
import { promisify } from 'util';

const execFileAsync = promisify(execFile);

// Path to the compiled exe. Set GTAV_MOD_MANAGER_EXE or defaults to PATH lookup.
const EXE = process.env.GTAV_MOD_MANAGER_EXE ?? 'GtavModManager.exe';

async function cli(...args) {
    try {
        const { stdout } = await execFileAsync(EXE, args, { timeout: 15_000 });
        return { ok: true, text: stdout.trim() };
    } catch (err) {
        const msg = err.stderr?.trim() || err.stdout?.trim() || err.message;
        return { ok: false, text: msg };
    }
}

function result(text, isError = false) {
    return { content: [{ type: 'text', text }], isError };
}

// ── Tool definitions ────────────────────────────────────────────────────────

const TOOLS = [
    {
        name: 'get_status',
        description: 'Get a summary of the mod manager state: how many mods are enabled, total mods, and whether the GTA V root is configured.',
        inputSchema: { type: 'object', properties: {} },
    },
    {
        name: 'list_mods',
        description: 'List all mods in the inventory with their name, type, and enabled/disabled status.',
        inputSchema: { type: 'object', properties: {} },
    },
    {
        name: 'enable_mod',
        description: 'Enable a mod so its files are linked into the GTA V directory. Accepts a partial, case-insensitive name.',
        inputSchema: {
            type: 'object',
            properties: {
                name: { type: 'string', description: 'Mod name or partial name' },
            },
            required: ['name'],
        },
    },
    {
        name: 'disable_mod',
        description: 'Disable a mod by removing its links from the GTA V directory. Files are kept in storage. Accepts a partial, case-insensitive name.',
        inputSchema: {
            type: 'object',
            properties: {
                name: { type: 'string', description: 'Mod name or partial name' },
            },
            required: ['name'],
        },
    },
    {
        name: 'scan_mods',
        description: 'Scan the GTA V directory for mod files that are not yet tracked in the inventory. Returns a list of candidates and detected frameworks (ScriptHookV, RPH, etc.).',
        inputSchema: { type: 'object', properties: {} },
    },
    {
        name: 'list_profiles',
        description: 'List saved mod profiles (named sets of enabled mods, e.g. "LSPDFR", "Freeplay").',
        inputSchema: { type: 'object', properties: {} },
    },
    {
        name: 'switch_profile',
        description: 'Switch to a saved mod profile, enabling and disabling mods as needed. Accepts a partial, case-insensitive profile name.',
        inputSchema: {
            type: 'object',
            properties: {
                name: { type: 'string', description: 'Profile name or partial name' },
            },
            required: ['name'],
        },
    },
    {
        name: 'emergency_restore',
        description: 'Copy all mod files from storage back to the GTA V directory and mark all mods as enabled. Use this if the game will not launch or mod files are missing after a crash or failed operation.',
        inputSchema: { type: 'object', properties: {} },
    },
];

// ── Server ──────────────────────────────────────────────────────────────────

const server = new Server(
    { name: 'gtav-mod-manager', version: '1.0.0' },
    { capabilities: { tools: {} } }
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({ tools: TOOLS }));

server.setRequestHandler(CallToolRequestSchema, async (req) => {
    const { name, arguments: args } = req.params;

    switch (name) {
        case 'get_status': {
            const r = await cli('status', '--json');
            return result(r.text, !r.ok);
        }
        case 'list_mods': {
            const r = await cli('list', '--json');
            return result(r.text, !r.ok);
        }
        case 'enable_mod': {
            const r = await cli('enable', args.name);
            return result(r.text, !r.ok);
        }
        case 'disable_mod': {
            const r = await cli('disable', args.name);
            return result(r.text, !r.ok);
        }
        case 'scan_mods': {
            const r = await cli('scan', '--json');
            return result(r.text, !r.ok);
        }
        case 'list_profiles': {
            const r = await cli('profile', 'list', '--json');
            return result(r.text, !r.ok);
        }
        case 'switch_profile': {
            const r = await cli('profile', 'switch', args.name);
            return result(r.text, !r.ok);
        }
        case 'emergency_restore': {
            const r = await cli('restore');
            return result(r.text, !r.ok);
        }
        default:
            return result(`Unknown tool: ${name}`, true);
    }
});

const transport = new StdioServerTransport();
await server.connect(transport);
