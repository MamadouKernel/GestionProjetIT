#!/usr/bin/env python3
"""
Script to update test specification columns K, L, M, N based on test characteristics
"""

import csv

def determine_test_type(test_id, module, role, point_verifier, criticite):
    """
    Determine if test requires unit test, functional test, or both
    Returns: (commentaire, test_unitaire, test_fonctionnel, procedure)
    """
    
    # Tests that are primarily functional (UI/Integration)
    functional_keywords = [
        'upload', 'affichage', 'visibilité', 'notification', 'mail',
        'écran', 'menu', 'page', 'formulaire', 'liste', 'colonne',
        'filtre', 'portefeuille', 'workflow', 'transition', 'génération pdf',
        'zone', 'champ', 'bouton', 'sélecteur', 'ouverture'
    ]
    
    # Tests that need unit tests (business logic, validation, calculation)
    unit_keywords = [
        'validation', 'blocage', 'calcul', 'vérification', 'contrôle',
        'règle', 'statut', 'automatique', 'enregistrement', 'modification',
        'création', 'suppression', 'mise à jour'
    ]
    
    # Security and access control tests need both
    security_keywords = [
        'sécurité', 'accès', 'interdiction', 'isolation', 'droits',
        'autorisation', 'permission', 'délégation'
    ]
    
    text_to_check = f"{module} {point_verifier}".lower()
    
    has_functional = any(keyword in text_to_check for keyword in functional_keywords)
    has_unit = any(keyword in text_to_check for keyword in unit_keywords)
    has_security = any(keyword in text_to_check for keyword in security_keywords)
    
    # Determine test types
    if has_security:
        test_unitaire = "Oui"
        test_fonctionnel = "Oui"
        commentaire = "Test de sécurité - validation unitaire et fonctionnelle requises"
    elif has_functional and has_unit:
        test_unitaire = "Oui"
        test_fonctionnel = "Oui"
        commentaire = "Test mixte - logique métier et interface utilisateur"
    elif has_functional:
        test_unitaire = "Non"
        test_fonctionnel = "Oui"
        commentaire = "Test fonctionnel UI uniquement"
    elif has_unit:
        test_unitaire = "Oui"
        test_fonctionnel = "Non"
        commentaire = "Test unitaire de logique métier"
    else:
        # Default: both needed for critical tests
        if criticite == "Bloquante":
            test_unitaire = "Oui"
            test_fonctionnel = "Oui"
            commentaire = "Test critique - couverture complète requise"
        else:
            test_unitaire = "Oui"
            test_fonctionnel = "Oui"
            commentaire = "Test standard"
    
    # Generate procedure based on test type
    if test_unitaire == "Oui" and test_fonctionnel == "Oui":
        procedure = "1. Exécuter les tests unitaires pour valider la logique métier\n2. Exécuter les tests fonctionnels UI\n3. Vérifier le résultat attendu\n4. Valider les cas limites"
    elif test_fonctionnel == "Oui":
        procedure = "1. Lancer l'application\n2. Exécuter le scénario de test fonctionnel\n3. Vérifier le résultat attendu dans l'interface\n4. Documenter les observations"
    elif test_unitaire == "Oui":
        procedure = "1. Exécuter le test unitaire correspondant\n2. Vérifier les assertions\n3. Valider la couverture de code\n4. Tester les cas d'erreur"
    else:
        procedure = "1. Exécuter le test\n2. Vérifier le résultat\n3. Documenter"
    
    return commentaire, test_unitaire, test_fonctionnel, procedure


def process_csv():
    """Process the CSV file and update columns"""
    input_file = 'Cahier_Recette_Avec_Resultats_Complet.csv'
    output_file = 'Cahier_Recette_Final.csv'
    
    rows = []
    
    with open(input_file, 'r', encoding='utf-8') as f:
        reader = csv.reader(f, delimiter=';')
        header = next(reader)
        
        # Structure attendue:
        # A-J: Process;ID test;Module;Rôle;Point à vérifier;Étapes;Résultat attendu;Statut;Criticité;Commentaires
        # K: Commentaires
        # L: Test Unitaire
        # M: Test Fonctionnel
        # N: Procédure de réalisation
        
        new_header = [
            'Process', 'ID test', 'Module', 'Rôle', 'Point à vérifier',
            'Étapes', 'Résultat attendu', 'Statut', 'Criticité', 'Commentaires',
            'Test Unitaire', 'Test Fonctionnel', 'Procédure de réalisation'
        ]
        rows.append(new_header)
        
        for row in reader:
            if len(row) < 9:
                continue
                
            process = row[0]
            test_id = row[1]
            module = row[2]
            role = row[3]
            point_verifier = row[4]
            etapes = row[5]
            resultat_attendu = row[6]
            statut = row[7]
            criticite = row[8]
            
            # Determine test types and generate content
            commentaire, test_unitaire, test_fonctionnel, procedure = determine_test_type(
                test_id, module, role, point_verifier, criticite
            )
            
            # Build new row
            new_row = [
                process,
                test_id,
                module,
                role,
                point_verifier,
                etapes,
                resultat_attendu,
                statut,
                criticite,
                commentaire,  # K: Commentaires
                test_unitaire,  # L: Test Unitaire
                test_fonctionnel,  # M: Test Fonctionnel
                procedure  # N: Procédure de réalisation
            ]
            
            rows.append(new_row)
    
    # Write output
    with open(output_file, 'w', encoding='utf-8', newline='') as f:
        writer = csv.writer(f, delimiter=';')
        writer.writerows(rows)
    
    print(f"✅ Fichier mis à jour: {output_file}")
    print(f"📊 {len(rows)-1} tests traités")
    print(f"\nStructure des colonnes:")
    print("  K: Commentaires")
    print("  L: Test Unitaire")
    print("  M: Test Fonctionnel")
    print("  N: Procédure de réalisation")


if __name__ == '__main__':
    process_csv()
