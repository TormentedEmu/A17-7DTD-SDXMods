using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SDX.Compiler;
using SDX.Core;

/// <summary>
/// TormentedEmu 2019 tormentedemu@gmail.com
/// Mod that will patch a line of code to EntityAlive::Init that will fix a NRE caused occasionally when logging in to the game
/// during the 'Create Player' stage.  Seen in modded and vanilla games randomly and at often the worst time possible.
/// </summary>
public class EmuCreatePlayerFix : IPatcherMod
{
  public bool Patch(ModuleDefinition module)
  {
    Logging.LogInfo("Start patch process..." + System.Reflection.MethodBase.GetCurrentMethod().ReflectedType);
    ModifyAssembly(module);
    Logging.LogInfo("Patch mod complete.");
    return true;
  }

  private void ModifyAssembly(ModuleDefinition module)
  {
    if (!ModifyEntityAlive(module))
      throw new Exception("Failed to find and modify the required method!");
  }

  private bool ModifyEntityAlive(ModuleDefinition module)
  {
    var eAlive = module.Types.FirstOrDefault(c => c.Name == "EntityAlive");
    if (eAlive == null)
    {
      Logging.LogError("Failed to find class EntityAlive.");
      return false;
    }

    var mInit = eAlive.Methods.First(f => f.Name == "Init");
    if (mInit == null)
    {
      Logging.LogError("Failed to find the method EntityAlive::Init.");
      return false;
    }

    Instruction branch = null;
    foreach (var inst in mInit.Body.Instructions)
    {
      if (inst.OpCode == OpCodes.Brfalse && inst.Operand.ToString().Contains(": ret"))
      {
        branch = inst;
        break;
      }
    }

    if (branch == null)
    {
      Logging.LogError("Failed to find the branch opcode to patch!");
      return false;
    }

    /*
     * Add one line of code to the end of the EntityAlive::Init method
     * 
      	this.MinEventContext.Self = this;

    as IL opcodes:
      84	00EC	ldarg.0
      85	00ED	ldflda	valuetype MinEventParams EntityAlive::MinEventContext
      86	00F2	ldarg.0
      87	00F3	stfld	class EntityAlive MinEventParams::Self
    */

    FieldDefinition minEvtCtx = eAlive.Fields.First(f => f.Name == "MinEventContext");
    FieldDefinition self = minEvtCtx.FieldType.Resolve().Fields.First(f => f.Name == "Self");

    var il = mInit.Body.GetILProcessor();
    var last = mInit.Body.Instructions.Last();

    var newTargInst = il.Create(OpCodes.Ldarg_0);
    il.InsertBefore(last, newTargInst);
    il.InsertBefore(last, il.Create(OpCodes.Ldflda, minEvtCtx));
    il.InsertBefore(last, il.Create(OpCodes.Ldarg_0));
    il.InsertBefore(last, il.Create(OpCodes.Stfld, self));
    branch.Operand = newTargInst; // change the target of this branch to our new opcode start so it falls outside the if statement block

    return true;
  }

  public bool Link(ModuleDefinition gameModule, ModuleDefinition modModule)
  {
    return true;
  }
}
