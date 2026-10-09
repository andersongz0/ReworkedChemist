"""Recoverable full-mod upgrade; no save or unrelated asset edits."""
import argparse, hashlib, json, shutil, subprocess
from pathlib import Path

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()

def main():
    parser=argparse.ArgumentParser()
    for key in ('previous','runtime','bridge','output','world-atlas','ai-plan'): parser.add_argument('--'+key,type=Path,required=True)
    args=parser.parse_args(); previous=args.previous.resolve(); out=args.output.resolve()
    builds=Path(__file__).resolve().parents[1]/'builds'
    if not out.is_relative_to(builds) or out==builds or out.exists(): raise ValueError('New unique build required.')
    proof=json.loads((previous/'package-verification.json').read_text()); name='Reworked Chemist - Venom Test'
    if proof['Stage']!='FullChemistPlayableTest' or proof['Actions']!=15 or proof['NewItemCount']!=11: raise ValueError('Complete previous package required.')
    for relative,expected in proof['ModFileHashes'].items():
        if sha(previous/name/relative)!=expected: raise ValueError('Previous package changed: '+relative)
    out.mkdir(); mod=out/name; shutil.copytree(previous/name,mod)
    replacements={'FFTModLoader.ReworkedChemist.VenomTest.dll':args.runtime,
                  'FFTModLoader.ContentExpansion.Native.dll':args.bridge}
    for relative,source in replacements.items(): shutil.copy2(source,mod/relative)
    atlas=json.loads((args.world_atlas/'world-bottle-atlas.json').read_text())
    expected_resources={'FFTIVC/data/enhanced/fftpack/tex/item/item_01.'+locale+'.tex' for locale in ('en','de','fr','ja')}
    if set(atlas['Resources'])!=expected_resources or len(atlas['Entries'])!=11 or not atlas['OriginalGlyphsPreserved'] or not atlas['OriginalClutsPreserved']:
        raise ValueError('Unverified native bottle atlas.')
    ai=json.loads(args.ai_plan.read_text())
    if ai['ExecutableSha256']!=proof['ExecutableSha256'] or (ai['SourceRva'],ai['OriginalCapacity'],ai['Capacity'],ai['OriginalRows'])!=(0x1872598,34,64,16) or (len(ai['Operands']),len(ai['BiasedOperands']),len(ai['Bounds']))!=(30,9,25):
        raise ValueError('Incomplete native AI expansion review.')
    additions=expected_resources|{'native/world-bottle-atlas.json','native/chemist-ai-expansion.json'}
    shutil.copy2(args.ai_plan,mod/'native/chemist-ai-expansion.json')
    for relative in sorted(expected_resources):
        source=args.world_atlas/relative
        if sha(source)!=atlas['Resources'][relative]['Sha256'] or source.stat().st_size!=0x7580:raise ValueError('Changed world atlas.')
        target=mod/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    shutil.copy2(args.world_atlas/'world-bottle-atlas.json',mod/'native/world-bottle-atlas.json')
    config=json.loads((mod/'ModConfig.json').read_text())
    config['ModAuthor']='ZeroDS'
    config['ProjectUrl']='https://github.com/andersongz0/ReworkedChemist'
    config['ModVersion']='0.2.43-items-selected-back-test'
    config['ModDescription']='Video049 selected-item target-cancel/Back regression: actual PE408FF8 recursive closure disables nested ListAction+22A; actual PE156964 root Show cannot restore its selection gate. Production closure now uses complete native PE1568EC/156A84/1569C0 cleanup and balances only the root Show, retaining nested shown-state.48 choose/target-cancel/Back cycles plus192 child exits and192 returned-parent exits verify nested selection and cursor cleanup with explicit resource/name/row/GPU leaf fixtures. Previous native reentry, four-menu positions/order, stock, targeting, AI, story, damage, interception, assets and saves preserved. Live gameplay awaits confirmation.'
    (mod/'ModConfig.json').write_text(json.dumps(config,indent=2)+'\n')
    subprocess.run(['dotnet','run','--project',str(builds.parent/'VenomNxdTests'),'-c','Release','--','--update-flask-descriptions',str(mod)],check=True)
    description_files={f'FFTIVC/data/enhanced/nxd/{family}.{locale}.nxd' for family in ('ability','item') for locale in ('en','de','fr','ja','ko','cs','ct')}
    medicine_files={f'native/{key}.item-medicine.bin' for key in ('fire-flask','ice-flask','thunder-flask')}
    for relative in medicine_files:(mod/relative).write_bytes(bytes((107,5,0)))
    definition=json.loads((builds.parent/'Alchemist.definition.json').read_text())
    chapters=definition['Balance']['ShopUnlockChapters']
    if list(chapters.values())!=[1,1,2,2,2,3,3,3,4,4,4]:raise ValueError('Unexpected story chapter policy.')
    common_files=set()
    for key,chapter in chapters.items():
        relative=f'native/{key}.item-common.bin';common_files.add(relative)
        data=bytearray((mod/relative).read_bytes())
        if len(data)!=12:raise ValueError('Invalid native item common record.')
        data[10]=1+4*(chapter-1);(mod/relative).write_bytes(data)
    hashes={str(p.relative_to(mod)).replace('\\','/'):sha(p) for p in mod.rglob('*') if p.is_file()}
    changes={r for r,h in hashes.items() if proof['ModFileHashes'].get(r)!=h}
    allowed=set(replacements)|{'ModConfig.json'}|additions|description_files|medicine_files|common_files
    if not {'FFTModLoader.ReworkedChemist.VenomTest.dll','ModConfig.json'}.issubset(changes) or not changes.issubset(allowed) or set(hashes)!=set(proof['ModFileHashes'])|additions: raise ValueError('Unexpected asset change.')
    proof.update(ModFileHashes=hashes,ModDirectory=str(mod),ChangedFiles=sorted(changes),RuntimeUnchanged=False,
                 AllElevenIconAssetsPreserved=True,ManualAutoContinueLoadHooks=True,NativeBridgeVersion=5,
                 NativeBridgeCapacity=64,NativeBridgeSha256=sha(args.bridge),GameplayTested=False,
                 GlassShatterGameplayVerified=False,DiagnosticRelease=True,FormationCrashFixed=False,
                 FullReworkedChemistReady=False,RequiresFormationReproduction=True,
                 NoCastingPoseNativeVerified=True,NoPersistentCasterEffectNativeVerified=True,
                 LegacyRsmEffectClassificationExpanded=True,OilMonsterVisualRemoved=True,
                 OilUsesProvisionalDarkCloud=True,ThirdCharacterCrashFixed=False,
                 NativeItemProjectileClassificationExpanded=True,
                 NativeBattleCollisionWaitLookupsExpanded=True,
                 NativeControllerCompletionFixtureVerified=True,
                 RendererAndEffectBytecodeFixtureOnly=True,
                 ActionCompletionDiagnostics=False,CompletionDiagnosticHookRemoved=True,
                 ChemistClosedMethodPreparationVerified=True,BlittableReadUsesSizeof=True,
                 ActualRuntimeConfigurationLogged=True,NativeTrajectoryPreparationVerified=True,
                 NativeProjectileStage1AndFlightVerified=True,
                 GameplayObserversAvoidReversePInvoke=True,NativeOwnerHelperTransportVerified=True,
                 FiberLocalNestedGameplayTransportVerified=True,ClrCorruptionWriterIdentified=False)
    proof.update(ScopedNativeBalloonNamesFixtureVerified=True,ElevenCustomWorldBottlesFixtureVerified=True,
                 ActualSmallMapBubbleCallsiteFixtureVerified=True,EnhancedBottleSourceCropConversionFixtureVerified=True,
                 ExistingSpriteDecoderConflictRegressionVerified=True,SharedSpriteDecoderEntryPreserved=True,
                 SharedDecoderCallerOsUnwindVerified=True,
                 ActualSelectionPacketBalloonRegressionVerified=True,NativeEightBitUvWrapRegressionVerified=True,
                 Native41Ac00CropWithCrtVerified=True,TransparentBottleEdgePreserved=True,
                 DisplayedUnitBalloonNativeFrameVerified=True,FullWidthPreviewBalloonBeforeByteQueueVerified=True,
                 BothNativeBalloonCallerPathsVerified=True,
                 OriginalWorldGlyphsAndClutsPreserved=True,WorldAtlasOriginalLocales=list(atlas['Resources']),
                 AddsManagedGameplayObservers=True,AddsIsolatedPotionUiObserver=True,
                 PotionSubmenuNativeCallerFixtureVerified=True,PotionFamilySelectedStockVerified=True,
                 PotionChildTaskCompletionResetVerified=True,PotionModalNativeTaskLifetimeVerified=True,
                 PotionTargetTaskDeferralVerified=True,PotionZeroStockDisabledVerified=True,
                 EnhancedPotionValidationWindowVerified=True,PotionFamilyNativeTrajectoryVerified=True,
                 PotionFamilyNativeHealingVerified=True,PotionVariantAdjacentThrowVerified=True,
                 PotionEnhancedInventoryColorsVerified=True,
                 EtherRemedySubmenusVerified=True,NativeMedicineFamilyEffectsVerified=True,
                 ThirteenMedicineTrajectoriesVerified=True,PrivateSevenRowInputStorageVerified=True,
                 MedicineCascadeLifecycleFixtureVerified=True,PassiveParentDrawingOnly=False,
                 MedicineCascadeRasterGameplayVerified=False,
                 MedicineCascadeValidNativeNamesVerified=True,MedicineCascadeParentRowTextOmitted=True,
                 ArtificialMedicineCascadeDisabled=True,HardEmptyMedicineConfirmationVerified=True,
                 NativeMedicineInterceptionFixtureVerified=True,ElementalMaxHpPercentNoFaithVerified=True,
                 UserConfirmedPreviousVersion='0.2.30-ai-integration-test',RequiresFormationReproduction=False)
    proof.update(ChemistAiCandidateBankExpanded=True,ChemistAiActualActions=25,ChemistAiRows=16,ChemistAiCapacity=64,
                 ChemistAiNativeExpansionSha256=sha(args.ai_plan),ChemistAiGuardedOperandCount=39,ChemistAiBoundCount=25,
                 ChemistAiLearnedStockEligibilityFixtureVerified=True,ChemistAiNpcNativeSupplyPolicyFixtureVerified=True,
                 ChemistAiNativeControllerTraversalFixtureVerified=True,ChemistAiNativeSelectionAndCommitFixtureVerified=True,
                 ChemistAiOriginalTargetPrefilterFixtureVerified=True,ChemistAiGameplayVerified=False)
    proof.update(ChemistShopStoryUnlockFixtureVerified=True,ChemistShopUnlockChapters=chapters,
                 ChemistShopNativeProgressThresholds={k:1+4*(v-1) for k,v in chapters.items()},
                 ElementalFixedDamage=5,ElementalTargetMaxHpPercent=15,ElementalPercentRounding='Ceiling',
                 ChemistAlliedAiUserConfirmed=True,ChemistEnemyAiGameplayVerified=False)
    proof.update(PreviousVersion='0.2.42-items-native-reopen-test',
                 ChemistAiNativeEligibilityHotPathVerified=True,ChemistAiEligibilityOsUnwindVerified=True,
                 ChemistAiEligibilityIdentityAndMutableGateVerified=True,
                 TurnStutterGameplayVerified=False)
    proof.update(MedicineNativeQuarterXScopeVerified=True,MedicinePositionWidthFraction=0.25,
                 MedicineParentSceneCloned=False,MedicinePositionGameplayVerified=False,
                 ChemistAiNativeLearningHotPathVerified=True,ChemistAiNonItemSelectionGateVerified=True)
    proof.update(MedicineIndependentFourthMenuFixtureVerified=True,MedicineOriginalItemsPreservedFixtureVerified=True,
                 MedicineFamilyTitlesFixtureVerified=True,MedicineSeparateControllerFocusFixtureVerified=True,
                 MedicineChildNativeComponentConstructorReviewed=True,MedicineFourthMenuGameplayVerified=False)
    proof.update(MedicineParentShowTimelineFixtureVerified=True,MedicineCharacterCascadeFixtureVerified=True,
                 MedicineUserVideoRegressionReviewed=True,MedicineChildForegroundSortFixtureVerified=True,
                 MedicineNativeAccumulatedRenderDepthVerified=True,MedicineReverseRenderTraversalReviewed=True,
                 MedicineIndependentRenderGroupsFixtureVerified=True,MedicineFinalBatchOrderFixtureVerified=True,
                 MedicineLiveNestedLayerCaptureReviewed=True,MedicineActualLayerSortFixtureVerified=True,
                 MedicineItemsCloseVideoReviewed=True,MedicineVisibleClosingParentFixtureVerified=True,
                 MedicineNativeShownStateCloseReopenVerified=True,MedicineItemsClosureGameplayVerified=False)
    proof.update(MedicineParentBackVisualScopeFixtureVerified=True,MedicineParentCursorRestoreFixtureVerified=True,
                 MedicineBackGameplayVerified=False)
    proof.update(MedicineNativeReentryUpdateGateFixtureVerified=True,MedicineNativeReentryGameplayVerified=False)
    proof.update(MedicineNestedNativeSelectionGateFixtureVerified=True,MedicineCompleteNativeListCloseFixtureVerified=True,
                 MedicineSelectedBackGameplayVerified=False)
    (out/'package-verification.json').write_text(json.dumps(proof,indent=2)+'\n')
    print('PASS: independent fourth native medicine menu release packaged; original Items, story, damage, AI, input, interception, icons, saves and identities preserved; gameplay layout pending.')

if __name__=='__main__': main()
